# CHOOGuard 클린 스타트 에픽·구현 명세 v3

**2026-09-19 · 신규 구축 · 검수 개정판 · 제품 실행 NOT_RUN**

빈 저장소에서 만드는 제품이며 초기화 이전 코드·에픽·구역 ID·성공 기록을 요구하지 않는다. 사용자 요구는 유지하고 누락된 기능 단위를 보완해 **11개 에픽·48개 작업·91개 제품 요구**로 연결한다.

## 에픽
| 에픽 | 목표 | 작업 수 |
|---|---|---:|
| [CS-BOOT](epics/CS-BOOT.md) | 새 Unity 제품의 부팅·입력·빌드 기반 | 3 |
| [CS-PACK](epics/CS-PACK.md) | 현장·기관 매뉴얼·자료 패키지 제작 | 4 |
| [CS-OPS](epics/CS-OPS.md) | 영속 상태를 가진 다기관 운영 커널 | 7 |
| [CS-WORLD](epics/CS-WORLD.md) | 현실 기반 철도 운영 공간 | 4 |
| [CS-PLAY](epics/CS-PLAY.md) | Unity 네이티브 RTS 운영 작업공간 | 7 |
| [CS-LAB](epics/CS-LAB.md) | 저장된 운영안의 분기·비교 실험 | 4 |
| [CS-MODES](epics/CS-MODES.md) | 매뉴얼 교육과 제약 기반 랜덤 실험 | 3 |
| [CS-SIM](epics/CS-SIM.md) | 다중 물리·행동 계산과 정량 검증 | 5 |
| [CS-SCRIPT](epics/CS-SCRIPT.md) | 선택 운영안의 근거 기반 대본 저작 | 4 |
| [CS-PROOF](epics/CS-PROOF.md) | 사용자 가치·현장 적합성 검증 | 4 |
| [CS-SHIP](epics/CS-SHIP.md) | 로컬 배포·복구·현장 확장 | 3 |

## 이번에 보완한 경계
입출력은 [타입·API](specs/02-wire-and-ports.md)와 [schemas](schemas/CommandIntent.schema.json), build는 [assembly](specs/01-build-and-assemblies.md), 저장은 [원자 계약](specs/03-durable-operations.md), 분기는 [복원·비교](specs/04-checkpoint-and-comparison.md), 물리는 [worker 계약](specs/05-worker-and-cosimulation.md)를 따른다. 화면은 [네이티브 화면 책임](specs/06-native-surfaces.md)을 따른다.

- CS-OPS.07: 실제 사람 작업량 계측 생산자.
- CS-PLAY.06: checkpoint/분기/A-B 비교의 실제 native presenter·prefab.
- CS-PLAY.07: 동일 코어의 연구용 표·타임라인. 세 번째 사용자 모드가 아니다.

## 시작과 병행
CS-BOOT.01의 첫 smoke는 후행 CompositionRoot를 요구하지 않는다. CS-BOOT.02에서 assembly/typed ports를 고정한 뒤 CS-PACK·CS-OPS·CS-WORLD·CS-PLAY·worker fixture가 필요한 계약을 소비한다. candidate는 명시된 test double로 단위 개발할 수 있다. integration은 선택 profile에서 활성화된 실제 산출물을 소비한다. 현장 qualification은 독립 자료·검수 범위가 필요하다. 전체 에픽 종료를 기다리는 관계로 자동 변환하지 않는다.

첫 완성 구간: 두 기관의 요청→자원 예약→업무→보고/인계→A 보존→B 변경→비교→대본 출력. 크게 보이는 맵이나 그럴듯한 UI만으로 완료하지 않는다.

## 수용 구분
문서 구조 / native 실행 / 사용자 효용 / 정량 모델 / 기관 활용 / 현실 갱신은 각각 [claim gate](contracts/claim-gates.json)로 검토한다. 이 패키지의 Python 검사는 Unity·물리·현장 성능시험이 아니다. 제품 인수시험과 48개 작업의 상태는 NOT_RUN / NOT_IMPLEMENTED다.


# CS-BOOT · 새 Unity 제품의 부팅·입력·빌드 기반

## CS-BOOT.01 · 새 프로젝트·의존성 잠금

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-BOOT.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** BuildBaseline {editorVersion, packageLockSha256, renderPipeline, backend, os, nativePlugins, buildReceiptRef}; 모든 값은 새 실행에서 기록.

### 필수 상세 명세
- [01-build-and-assemblies](specs/01-build-and-assemblies.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/GraphicsSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/ChooGuard/Scenes/Bootstrap.unity`
- `docs/build/baseline.json`

### 선행 산출물과 소비 단계
선행 구현 산출물 없음. 현재 작업의 입력과 안전 경계는 확인한다.

### 구현 절차
1. Unity 6 LTS 계열의 URP 3D 프로젝트를 비어 있는 작업 폴더에 생성한다. 정확한 패치·OS·backend는 설치된 정식 조합으로 선택해 기록한다.
2. uGUI/TMP·Input System·Test Framework를 새로 확인/도입하고 전체 lock을 커밋한다. 한글 font asset은 공급경로만 기록하고 이 명세에 폰트 바이너리를 넣지 않는다.
3. Bootstrap 선행 산출물은 도메인·CompositionRoot가 없는 정적 카메라·Canvas·EventSystem의 smoke scene이다. CS-BOOT.02의 실제 CompositionRoot는 별도 installer가 나중에 조립한다. 이 단계는 존재하지 않는 후행 클래스를 요구하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-BOOT.01-P**

Given: 빈 디렉터리, Camera/Canvas/EventSystem 각1  
When: 새 native smoke 부팅  
Then: composition-root 참조0; EventSystem1; 신규 version receipt  
Result: NOT_RUN

**TEST-CS-BOOT.01-N**

Given: 존재하지 않는 local DLL  
When: 새 native smoke 부팅  
Then: 빌드 미실행/실패; 과거 receipt 재사용0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/BOOT/Boot01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-066.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-BOOT.02 · assembly 경계와 신규 공개 계약

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-BOOT.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** IOperationsPort.PreviewAsync(CommandIntent,CancellationToken); SubmitAsync(CommandIntent,CancellationToken); ReadReceiptAsync(ReceiptKey,CancellationToken); ReadProjectionAsync(ProjectionQuery,CancellationToken). 정확 타입은 specs/02-wire-and-ports.md.

### 필수 상세 명세
- [01-build-and-assemblies](specs/01-build-and-assemblies.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Contracts/ChooGuard.Contracts.asmdef`
- `Assets/ChooGuard/Domain/ChooGuard.Domain.asmdef`
- `Assets/ChooGuard/Application/ChooGuard.Application.asmdef`
- `Assets/ChooGuard/Presentation/ChooGuard.Presentation.asmdef`
- `Assets/ChooGuard/Contracts/OperationsPorts.cs`
- `Assets/ChooGuard/App/CompositionRoot.cs`
- `Assets/ChooGuard/Editor/InstallCompositionRoot.cs`
- `Assets/ChooGuard/Content/ChooGuard.Content.asmdef`
- `Assets/ChooGuard/Persistence/ChooGuard.Persistence.asmdef`
- `Assets/ChooGuard/World/ChooGuard.World.asmdef`
- `Assets/ChooGuard/Experiments/ChooGuard.Experiments.asmdef`
- `Assets/ChooGuard/Scenarios/ChooGuard.Scenarios.asmdef`
- `Assets/ChooGuard/Simulation/ChooGuard.Simulation.asmdef`
- `Assets/ChooGuard/Authoring/ChooGuard.Authoring.asmdef`
- `Assets/ChooGuard/App/ChooGuard.App.asmdef`
- `Assets/ChooGuard/Editor/ChooGuard.Editor.asmdef`

### 선행 산출물과 소비 단계
- `CS-BOOT.01:candidate` → `CS-BOOT.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. Contracts와 Domain은 UnityEngine에 의존하지 않는 경계로 만든다. 저장과 계산은 port로 연결한다.
2. CommandIntent, CommandReceipt, SessionProjection, IOperationsPort를 CONTRACTS.md의 신규 모델대로 정의한다.
3. CompositionRoot는 한 run당 한 state writer를 생성한다. 각 외부 worker는 별도 기능을 확인하기 전 생성하지 않는다.
4. contracts/assembly-layout.json의 모든 모듈과 test assembly 경계를 만들고 각 신규 C# 파일이 정확히 한 assembly에 속하는지 검사한다.
5. specs/02-wire-and-ports.md의 타입·오류·비동기 계약을 사용한다. ReadReceipt는 runId뿐 아니라 requesterId를 포함한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-BOOT.02-P**

Given: 13 module roots와 1개 Contracts DTO  
When: 경계 컴파일  
Then: Domain→Contracts만; 모든 runtime C# 소속1  
Result: NOT_RUN

**TEST-CS-BOOT.02-N**

Given: World.cs를 asmdef 밖으로 이동  
When: 경계 컴파일  
Then: PREDEFINED_ASSEMBLY_LEAK  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/BOOT/Boot02Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-066.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: AUD-UNITY-ASSEMBLY. [출처 등록부](reference/sources.json).

---

## CS-BOOT.03 · 새 테스트·빌드 실행 경로

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-BOOT.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TestReceipt {testSet, commit, environment, startedAt, command, resultFiles, status, notRunReason}; CI credential은 저장소에 넣지 않는다.

### 필수 상세 명세
- [01-build-and-assemblies](specs/01-build-and-assemblies.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `scripts/Run-UnityTests.ps1`
- `scripts/Build-Player.ps1`
- `.github/workflows/verify.yml`
- `Assets/ChooGuard/Tests/EditMode/ChooGuard.EditModeTests.asmdef`
- `Assets/ChooGuard/Tests/PlayMode/ChooGuard.PlayModeTests.asmdef`
- `Assets/ChooGuard/Editor/BuildCommands.cs`
- `Assets/ChooGuard/Tests/Acceptance/ChooGuard.AcceptanceTests.asmdef`
- `scripts/Run-PlayerAcceptance.ps1`

### 선행 산출물과 소비 단계
- `CS-BOOT.01:candidate` → `CS-BOOT.03:integration` / 조건 `ALWAYS`.
- `CS-BOOT.02:candidate` → `CS-BOOT.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. UNITY_EDITOR_PATH와 PROJECT_PATH를 입력받아 Test Framework를 실행하고 결과 XML·로그·exit code를 보존한다.
2. 라이선스/장치가 없는 CI는 미실행으로 실패 또는 중립 상태를 명시하고 임의 PASS를 만들지 않는다.
3. 일반 compile smoke와 Player의 렌더·IME·입력·디스크 시험을 분리한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-BOOT.03-P**

Given: 실패 시험1개와 새 결과 경로  
When: test runner 실행  
Then: 실패 XML과 nonzero exit  
Result: NOT_RUN

**TEST-CS-BOOT.03-N**

Given: 시험0개 또는 stale XML  
When: test runner 실행  
Then: NO_TESTS_OR_STALE_RESULT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/BOOT/Boot03Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSBOOT03PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-BOOT.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-061.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: AUD-UNITY-ASSEMBLY. [출처 등록부](reference/sources.json).


# CS-PACK · 현장·기관 매뉴얼·자료 패키지 제작

## CS-PACK.01 · 새 공간·기관·시간·데이터 식별자

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SiteBundle {id,revision,frames,regions,portals,entities,sourceRefs,qualification}; ScenarioSpec와 OperationalPlan은 별도 revision.

### 필수 상세 명세
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `Assets/ChooGuard/Contracts/ContentTypes.cs`
- `Assets/ChooGuard/Content/ContentValidator.cs`
- `content/schemas/site.schema.json`
- `content/schemas/scenario.schema.json`
- `content/fixtures/two-agency.json`

### 선행 산출물과 소비 단계
- `CS-BOOT.02:candidate` → `CS-PACK.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 새 stable ID를 발급하고 표시 이름과 분리한다. 구역 수·ID 집합은 실제 목표 범위로 정의하며 과거 ID 목록을 요구하지 않는다.
2. SI 단위, 좌표 frame, simulation tick, event sequence, authored/observed/received 시간을 분리한다.
3. SiteBundle·ScenarioSpec·OperationalPlan의 참조/버전을 검사하고 원본을 immutable로 보관한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PACK.01-P**

Given: 같은 표시명 기관 a,b와 양수 자원  
When: 콘텐츠 등록  
Then: ID a,b 유지; 이름 병합0  
Result: NOT_RUN

**TEST-CS-PACK.01-N**

Given: 미존재 region-z  
When: 콘텐츠 등록  
Then: UNKNOWN_REFERENCE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PACK/Pack01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-015, REQ-060.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: STD-JSONLD, STD-PROV. [출처 등록부](reference/sources.json).

---

## CS-PACK.02 · 기관 매뉴얼을 검수 가능한 규칙으로 구성

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** RuleClause {id,revision,kind,scope,guard,effect,handoff,exception,sourceLocator,review}; DTO는 예외를 검토자료로 유지.

### 필수 상세 명세
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `Assets/ChooGuard/Contracts/RuleTypes.cs`
- `Assets/ChooGuard/Content/RuleCatalog.cs`
- `content/schemas/rule.schema.json`
- `content/rules/catalog.json`

### 선행 산출물과 소비 단계
- `CS-PACK.01:candidate` → `CS-PACK.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 원문 locator·판본·기관·상황·단위·조건·권한·효과·인계·예외를 필드로 만든다.
2. REQUIRED, DISCRETIONARY, ADVISORY, INVARIANT, UNKNOWN을 구분한다.
3. 승인 주체와 revision 결속이 없는 후보는 official rule로 활성화하지 않는다. 규칙 변경은 의존 초안의 자격만 만료시킨다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PACK.02-P**

Given: 기관a 범위와 revision2 승인  
When: rule 활성화  
Then: a/rev2에만 적용  
Result: NOT_RUN

**TEST-CS-PACK.02-N**

Given: rev3에 rev2 승인 재사용  
When: rule 활성화  
Then: STALE_REVIEW  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PACK/Pack02Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-031, REQ-033, REQ-034.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: MAN-KORAIL, MAN-MEDICAL, MAN-SOP. [출처 등록부](reference/sources.json).

---

## CS-PACK.03 · 무료 원본 입고와 파일 신뢰경계

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** AssetReceipt {id,sourceUrl,tier,rawHash,formats,rigClips,parts,importStatus,limits}; 실제 바이너리가 없으면 rawHash=null.

### 필수 상세 명세
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `Assets/ChooGuard/Content/AssetIntakeValidator.cs`
- `sources/assets/catalog.json`
- `sources/assets/receipts.schema.json`

### 선행 산출물과 소비 단계
- `CS-PACK.01:candidate` → `CS-PACK.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 무료판별 파일 목록을 확인하고 원본은 격리 경로에서 hash·크기·압축경로·실행코드 유무를 검사한다.
2. 페이지 확인과 원본 취득·Unity 임포트·현장 수용을 다른 필드로 둔다.
3. 표현용 기하에서 정원·물성·능력을 추론하지 않는다. 실제 무료 다운로드 상태는 새 환경에서 재확인한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PACK.03-P**

Given: 무료 archive의 서로 다른 파일2개  
When: 입고 검사  
Then: raw bytes·해시·tier 분리  
Result: NOT_RUN

**TEST-CS-PACK.03-N**

Given: ../escape.cs 또는 A.fbx/a.fbx  
When: 입고 검사  
Then: UNSAFE_ARCHIVE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PACK/Pack03Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-062, REQ-063, REQ-064.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: FREE-001, FREE-010, FREE-011, FREE-018, MAT-BIPA-BUSAN3, MAT-KTX-I. [출처 등록부](reference/sources.json).

---

## CS-PACK.04 · 실제 현장 입력·현실 관측 갱신

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Observation {targetId,observedAt,receivedAt,value,unit,sourceHash,quality}; 현실 자료 없음은 기술 개발의 전역 blocker가 아님.

### 필수 상세 명세
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `content/sites/first-site/source-manifest.json`
- `content/sites/first-site/observation-policy.json`
- `Assets/ChooGuard/Content/RealityBaselineUpdater.cs`

### 선행 산출물과 소비 단계
- `CS-PACK.01:candidate` → `CS-PACK.04:integration` / 조건 `ALWAYS`.
- `CS-PACK.02:candidate` → `CS-PACK.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 허용된 공간·독립 기준 치수·기관 역할·작업시간 근거를 수집하고 관측/문헌/가정을 분리한다.
2. 관측 시간·수신 시간·대상 revision을 검사한다. 역순 갱신은 최신값을 조용히 덮지 않는다.
3. 가상 문 개방·훈련 사건을 실제 시설 관측으로 저장하지 않는다. 갱신한 SiteBundle은 새 revision을 만든다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PACK.04-P**

Given: 관측시각 t10 뒤 t12 실제관측  
When: reality 갱신  
Then: 새 revision; 과거 run 불변  
Result: NOT_RUN

**TEST-CS-PACK.04-N**

Given: 가상 run의 문 이벤트  
When: reality 갱신  
Then: NOT_REALITY_OBSERVATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PACK/Pack04Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-050.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: MAN-KORAIL, STD-DTC, STD-SOSA. [출처 등록부](reference/sources.json).


# CS-OPS · 영속 상태를 가진 다기관 운영 커널

## CS-OPS.01 · 신규 run·단일 writer·명령 상태머신

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** CommandIntent→CommandReceipt, stateVersion/readSet; 수락은 수행완료와 다르다.

### 필수 상세 명세
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [03-durable-operations](specs/03-durable-operations.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Application/OperationsSession.cs`
- `Assets/ChooGuard/Application/CommandDispatcher.cs`
- `Assets/ChooGuard/Domain/RunState.cs`

### 선행 산출물과 소비 단계
- `CS-BOOT.02:candidate` → `CS-OPS.01:integration` / 조건 `ALWAYS`.
- `CS-PACK.01:candidate` → `CS-OPS.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. run별 mailbox와 single-writer queue를 만들고 순서는 simulation tick·priority·monotonic sequence로 고정한다.
2. Preview는 side effect 없이 현재 guard를 평가한다. Submit은 requester와 acting team을 구분해 재검사한다.
3. 같은 key+payload는 같은 receipt, 다른 payload는 INTENT_CONFLICT를 반환한다. 인메모리 double은 기능시험 전용이다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.01-P**

Given: 동일 run/requester/key/payload 2회  
When: Submit 재전송  
Then: receipt 동일1개; 효과1회  
Result: NOT_RUN

**TEST-CS-OPS.01-N**

Given: 동일 key 다른 target  
When: Submit 재전송  
Then: INTENT_CONFLICT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-001, REQ-071.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-OPS.02 · SQLite 저장·원자 예약·outbox

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** IRunStore.CommitAsync(CommitBatch,CancellationToken) → Task<CommitReceipt>. 정확 필드는 schemas/CommitBatch.schema.json 및 schemas/CommitReceipt.schema.json; K03 원자성 규칙 적용.

### 필수 상세 명세
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [03-durable-operations](specs/03-durable-operations.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Persistence/SqliteRunStore.cs`
- `Assets/ChooGuard/Persistence/SqliteProvider.cs`
- `Assets/ChooGuard/Persistence/Schema.sql`
- `Assets/ChooGuard/Domain/ReservationPlanner.cs`
- `Assets/ChooGuard/Application/OutboxDispatcher.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.01:candidate` → `CS-OPS.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 대상 Unity Player의 managed/native SQLite 조합을 smoke-test하고 pin한다. 플랫폼별 P/Invoke 성공을 확인한다.
2. BEGIN IMMEDIATE 경계에서 run revision·현재 자원 가용량을 재검사한다. 팀 구성원·승무원은 고유 ID로 중복점유를 검사한다.
3. 이벤트·예약·receipt·projection revision·outbox를 원자적으로 저장하고 commit 성공 뒤에만 acceptance를 게시한다.
4. 외부 전달은 재시도 가능하지만 jobId/resultId로 멱등 적용한다. 디스크 실패 후 candidate 메모리 상태를 게시하지 않는다.
5. 배포 SQLite는 3.51.3 이상 또는 공식 WAL-reset 수정 backport를 바이너리 버전·source-id·해시로 입증한다. 단순 managed wrapper 버전으로 판정하지 않는다.
6. 명령·예약은 하나의 actor-serialized connection에서 처리하고 WAL/FULL/foreign_keys 설정을 확인한다. 백업은 snapshot API와 blob pin으로 수행한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.02-P**

Given: capacity3, jobA 예약2, jobB 요청2  
When: 원자 예약  
Then: jobB 거부; 총예약2; 나머지 자원 미점유  
Result: NOT_RUN

**TEST-CS-OPS.02-N**

Given: commit 직전 예외  
When: 원자 예약  
Then: 전체 rollback; ACCEPTED0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops02Tests.cs` · NOT_RUN
- PLAYER_DISK: `qualification/player/CS-OPS.02-disk.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-024, REQ-071.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-TAPAAL, AUD-SQLITE-WAL. [출처 등록부](reference/sources.json).

---

## CS-OPS.03 · 기관 권한·지원요청·인계

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** AuthorityGrant {holder,agency,scope,revision,ruleRef}; RequestSupport와 AssignTask는 구별.

### 필수 상세 명세
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [03-durable-operations](specs/03-durable-operations.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/AuthorityPolicy.cs`
- `Assets/ChooGuard/Domain/SupportRequest.cs`
- `Assets/ChooGuard/Domain/HandoverPolicy.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.02:candidate` → `CS-OPS.03:integration` / 조건 `ALWAYS`.
- `CS-PACK.02:candidate` → `CS-OPS.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 작성자의 가상 설계권과 기관의 업무권을 구분한다.
2. 기관내 지시와 기관간 요청은 서로 다른 event로 처리한다.
3. 인계에는 원 권한 revision·범위·상대 확인·사유를 요구하며 후착 도착 자체는 전역 권한을 바꾸지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.03-P**

Given: agencyA command와 agencyB resource  
When: 외부 support 요청  
Then: B 접수 전 배정0  
Result: NOT_RUN

**TEST-CS-OPS.03-N**

Given: A가 B 자원을 직접확정  
When: 외부 support 요청  
Then: AUTHORITY_DENIED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops03Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-021, REQ-022.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: MAN-KORAIL, MAN-SOP. [출처 등록부](reference/sources.json).

---

## CS-OPS.04 · 기관별 정보·보고·인계 상태

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Message {id,sender,recipient,observedTick,receivedTick,expiry,payloadRef,ack}; projection은 read-only.

### 필수 상세 명세
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [03-durable-operations](specs/03-durable-operations.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/MessageLifecycle.cs`
- `Assets/ChooGuard/Domain/KnowledgeState.cs`
- `Assets/ChooGuard/Application/AgencyProjection.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.03:candidate` → `CS-OPS.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. messageId·관측시각·전달상태·TTL·수신기관을 기록한다.
2. 중복은 같은 의미효과를 재적용하지 않고 역순·상충 보고는 UNKNOWN/CONFLICTED 상태로 남긴다.
3. 작성자 전체 분석 projection이 기관 knowledge를 수정하지 못하게 한다. 미래 상태를 과거 복기의 지식으로 사용하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.04-P**

Given: message m1 두번 도착  
When: ack 처리  
Then: 지식효과1회; received 원시기록 보존  
Result: NOT_RUN

**TEST-CS-OPS.04-N**

Given: TTL 만료후 guard 충족 요청  
When: ack 처리  
Then: STALE_INFORMATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops04Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-011, REQ-027, REQ-028.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-OPS.05 · 업무 의존·공간 접근·예약 생명주기

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.05 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TaskState {lifecycle,guards,reasons,reservations,outcome,handoff}; GuardResult는 truth/status와 evidence refs를 가진다.

### 필수 상세 명세
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [03-durable-operations](specs/03-durable-operations.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/WorkflowEngine.cs`
- `Assets/ChooGuard/Domain/ResourceLifecycle.cs`
- `Assets/ChooGuard/Domain/TaskConditions.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.04:candidate` → `CS-OPS.05:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 요청→배정→준비/이동→수행→보고/인계→반환 중 업무별 필요한 단계를 명시한다.
2. 작업 소요시간은 근거가 있는 조건부 입력으로 받는다. 합성 값은 검증된 현장 예측과 분리한다.
3. 팀 분리/결합·취소·교대·재보급은 능력과 자원 점유를 다시 검사한다. 수용·의료 인계는 운영상태이며 임상 판단을 생성하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.05-P**

Given: 선행 t1 완료; 장비reserved; report pending  
When: t2 평가  
Then: WAITING_REPORT; 자원보존  
Result: NOT_RUN

**TEST-CS-OPS.05-N**

Given: 다른 task 예약 취소  
When: t2 평가  
Then: RESERVATION_OWNERSHIP_MISMATCH  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops05Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-012, REQ-023, REQ-025, REQ-026, REQ-029, REQ-045.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: MAN-MEDICAL, MAN-SOP. [출처 등록부](reference/sources.json).

---

## CS-OPS.06 · 다축 원인·교착·형식 모델 대조

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.06 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Reason {axis,code,subject,causes,evidence,resolutionConditions}; 설명 모델은 권한·결과를 변경하지 못함.

### 필수 상세 명세
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [03-durable-operations](specs/03-durable-operations.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Domain/ReasonEngine.cs`
- `Assets/ChooGuard/Domain/WaitForAnalyzer.cs`
- `benchmarks/operations/model-projection.json`

### 선행 산출물과 소비 단계
- `CS-OPS.05:candidate` → `CS-OPS.06:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 진행상태와 원인축을 별개로 출력하며 다수 원인을 보존한다.
2. wait-for graph에서 cycle을 탐지하되 외부 도착 예정/불충분 정보와 확정 교착을 구별한다.
3. TAPAAL에 투영 가능한 부분의 trace를 비교하고 timeout·범위 제한은 미검증으로 보고한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.06-P**

Given: taskA→B→A wait graph  
When: 대기 분석  
Then: cycle 설명; 미래 외부자원과 분리  
Result: NOT_RUN

**TEST-CS-OPS.06-N**

Given: 등록되지 않은 원인 code  
When: 대기 분석  
Then: UNKNOWN_REASON_CODE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/Ops06Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-013, REQ-030, REQ-041.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-TAPAAL. [출처 등록부](reference/sources.json).

---

## CS-OPS.07 · 사람 작업량·도움·계산 대기 계측

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-OPS.07 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ActivityInterval {participantCode, activityId, category, startMonotonicUs, endMonotonicUs, observedWallAt, consentRef, assistance, completeness}; 가상시각을 사람 시간으로 집계하지 않음.

### 필수 상세 명세
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [03-durable-operations](specs/03-durable-operations.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Application/ActivityRecorder.cs`
- `Assets/ChooGuard/Persistence/ActivityStore.cs`
- `Assets/ChooGuard/Contracts/ActivityTypes.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.02:candidate` → `CS-OPS.07:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 동의한 가명 참가자에 대해 작업 시작·전환·종료를 기록한다. 전역 키로거나 문장 원문 수집은 하지 않는다.
2. 동일 참가자의 중첩 구간은 합집합으로 계산하고 기관 회신 대기·무인 계산은 별도 열에 둔다. 강제종료의 열린 구간은 검열된 구간으로 남긴다.
3. UI의 입력·복기·출력 수정과 개발자 도움을 빠짐없이 같은 caseId에 결속하고 CSV/JSON으로 내보낸다. 동의 철회·보존정책을 적용한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-OPS.07-P**

Given: 한 사람 [0,10),[5,15)분; 다른사람[0,10)  
When: 작업량 집계  
Then: 합25인분; 같은사람15분  
Result: NOT_RUN

**TEST-CS-OPS.07-N**

Given: 종료 없는 interval  
When: 작업량 집계  
Then: CENSORED; 완료자 평균에서 숨김0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/OPS/CSOPS07Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-079.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).


# CS-WORLD · 현실 기반 철도 운영 공간

## CS-WORLD.01 · 처음부터 만드는 2공간·문 fixture

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** WorldAnchorMap {entityId,transformRef,frameId,representationKind}; Unity Transform은 표시 수단.

### 필수 상세 명세
- [01-build-and-assemblies](specs/01-build-and-assemblies.md)
- [06-native-surfaces](specs/06-native-surfaces.md)
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)

### 새 구현 경로
- `Assets/ChooGuard/Editor/FixtureBuilder.cs`
- `Assets/ChooGuard/World/WorldEntityAnchor.cs`
- `Assets/ChooGuard/Scenes/OperationsFixture.unity`

### 선행 산출물과 소비 단계
- `CS-BOOT.02:candidate` → `CS-WORLD.01:integration` / 조건 `ALWAYS`.
- `CS-PACK.01:candidate` → `CS-WORLD.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 1m 비대칭 기준체와 두 공간·문 하나를 새 Scene에 만든다. 모든 고유 ID는 현 기준에서 발급한다.
2. visual/collider/navigation/semantic 레이어를 분리한다. 문 열림은 도메인 projection을 받아 표시한다.
3. 실측 부산역으로 표시하지 않고 SYNTHETIC_FIXTURE 라벨을 붙인다. 초기 fixture는 실제 자료 취득을 기다리지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-WORLD.01-P**

Given: 1m 비대칭 기준체와 2방1문  
When: fixture 생성  
Then: 좌표 축·문 피벗·고유ID 확인  
Result: NOT_RUN

**TEST-CS-WORLD.01-N**

Given: 실측 표식 강제  
When: fixture 생성  
Then: SYNTHETIC_NOT_FIELD  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/WORLD/World01Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSWORLD01PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-WORLD.01.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-001.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-WORLD.02 · 첫 철도 공간과 무료 시각자산 제작

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** GeometryReceipt {assetId,sourceHash,transform,units,parts,residuals,scope}; 실제 맵 수용에는 현장 입력이 추가로 필요.

### 필수 상세 명세
- [01-build-and-assemblies](specs/01-build-and-assemblies.md)
- [06-native-surfaces](specs/06-native-surfaces.md)
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)

### 새 구현 경로
- `Assets/ChooGuard/Editor/AssetImportPolicy.cs`
- `Assets/ChooGuard/World/CoordinateValidator.cs`
- `Assets/ChooGuard/Art/first-site.asset-manifest.json`
- `content/sites/first-site/geometry.json`
- `Assets/ChooGuard/Scenes/FirstSite.unity`

### 선행 산출물과 소비 단계
- `CS-WORLD.01:candidate` → `CS-WORLD.02:integration` / 조건 `ALWAYS`.
- `CS-PACK.03:candidate` → `CS-WORLD.02:integration` / 조건 `ALWAYS`.
- `CS-PACK.04:candidate` → `CS-WORLD.02:qualification` / 조건 `NAMED_SITE_ACCURACY`.

### 구현 절차
1. 무료 원본을 하나씩 격리 임포트하여 단위·축·rig·clip·문 피벗·LOD·재질·충돌을 검사한다.
2. 핵심 공간은 실제 자료의 근거별로 제작하고 신경복원 후보는 COLMAP 대조와 독립 치수를 함께 사용한다.
3. 관측 부족이면 합성/추정 표현과 필요한 입력을 분리한다. 독립 치수 없이 신경망 confidence를 실측 정확도로 쓰지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-WORLD.02-P**

Given: 첫현장 scene와 source/geometry receipt  
When: 입고/장면대조  
Then: Scene·geometry·source가 같은 ID/축척  
Result: NOT_RUN

**TEST-CS-WORLD.02-N**

Given: source 없는 3D 치수  
When: 입고/장면대조  
Then: UNVERIFIED_GEOMETRY  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/WORLD/World02Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSWORLD02PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-WORLD.02.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-014, REQ-017, REQ-018.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: FREE-001, FREE-003, FREE-018, MAT-BIPA-BUSAN3, MAT-KTX-I, TECH-MAPANYTHING, TECH-UNITY-FBX. [출처 등록부](reference/sources.json).

---

## CS-WORLD.03 · 운영 객체·월드마커·층별 보기

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SessionProjection: schemas/SessionProjection.schema.json의 entitiesRef/tasksRef/reasonsRef를 해석하며 run/revision/viewScope를 확인한다. 표현 계층은 domain을 직접 쓰지 않는다.

### 필수 상세 명세
- [01-build-and-assemblies](specs/01-build-and-assemblies.md)
- [06-native-surfaces](specs/06-native-surfaces.md)
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)

### 새 구현 경로
- `Assets/ChooGuard/World/WorldProjectionApplier.cs`
- `Assets/ChooGuard/World/WorldMarkerPool.cs`
- `Assets/ChooGuard/World/FloorVisibility.cs`

### 선행 산출물과 소비 단계
- `CS-WORLD.01:candidate` → `CS-WORLD.03:integration` / 조건 `ALWAYS`.
- `CS-OPS.01:candidate` → `CS-WORLD.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 최신 run/revision의 projection만 main thread에 반영한다. 다른 branch 결과를 적용하지 않는다.
2. 층·지붕·거리 LOD는 renderer만 바꾸고 물리/예약/지식은 바꾸지 않는다.
3. marker는 선택 우선으로 풀링하고 카메라 뒤/다른 층/기관 정보 범위를 처리한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-WORLD.03-P**

Given: run-a revision2 projection  
When: main-thread apply  
Then: a/rev2만 표시  
Result: NOT_RUN

**TEST-CS-WORLD.03-N**

Given: run-b 응답 또는 a/rev1  
When: main-thread apply  
Then: STALE_OR_FOREIGN_PROJECTION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/WORLD/World03Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSWORLD03PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-WORLD.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-016.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-UNITY-LOAD. [출처 등록부](reference/sources.json).

---

## CS-WORLD.04 · 구역 준비·로딩·차량 프레임 연결

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** RegionReadiness {regionId,visual,collision,path,state}; DockLink {vehicleFrame,siteFrame,stopConfirmed,doorState}.

### 필수 상세 명세
- [01-build-and-assemblies](specs/01-build-and-assemblies.md)
- [06-native-surfaces](specs/06-native-surfaces.md)
- [07-content-and-rule-contract](specs/07-content-and-rule-contract.md)

### 새 구현 경로
- `Assets/ChooGuard/World/RegionLoader.cs`
- `Assets/ChooGuard/World/RegionReadiness.cs`
- `Assets/ChooGuard/World/VehicleFrameBinding.cs`

### 선행 산출물과 소비 단계
- `CS-WORLD.03:candidate` → `CS-WORLD.04:integration` / 조건 `ALWAYS`.
- `CS-PACK.01:candidate` → `CS-WORLD.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 작은 구간은 통째로 로드하며 확장시 additive loading을 적용한다. logical lifetime은 Scene과 분리한다.
2. 진입은 collision·경로·필수 state가 준비된 경우에만 허용한다. 로딩 실패는 해당 구간만 표시한다.
3. 정차·도킹·문 상태를 연결하며 객실 local frame과 현장 frame의 변환을 검증한다. 운행 범위는 별도 capability로 선언한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-WORLD.04-P**

Given: logical ready, visual loading  
When: 카메라 전환  
Then: sim 이동결과 불변  
Result: NOT_RUN

**TEST-CS-WORLD.04-N**

Given: required computational geometry 없음  
When: 카메라 전환  
Then: RUN_LOAD_BLOCKED; 현장지연 수치로 대체0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/WORLD/World04Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSWORLD04PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-WORLD.04.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-020.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-UNITY-LOAD. [출처 등록부](reference/sources.json).


# CS-PLAY · Unity 네이티브 RTS 운영 작업공간

## CS-PLAY.01 · native 입력 소유권·IME

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** InputOwner {context,pointerId,focus,modal}; 소비한 event를 하위 입력에 재전달하지 않는다.

### 필수 상세 명세
- [06-native-surfaces](specs/06-native-surfaces.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Input/InputContextRouter.cs`
- `Assets/ChooGuard/Presentation/Input/PointerCaptureOwner.cs`
- `Assets/ChooGuard/Presentation/Input/Operations.inputactions`

### 선행 산출물과 소비 단계
- `CS-BOOT.01:candidate` → `CS-PLAY.01:integration` / 조건 `ALWAYS`.
- `CS-WORLD.01:candidate` → `CS-PLAY.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 모달→텍스트/IME→HUD→월드→카메라 순서로 입력 소유권을 고정한다.
2. pointer-down의 소유자를 up/cancel까지 유지하고 UI drag가 월드 선택으로 전환되지 않게 한다.
3. TMP 입력·IME 조합 중 Space/WASD/Esc를 game action으로 전달하지 않는다. 실제 Player에서 조합/확정/취소 시험을 한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PLAY.01-P**

Given: UI에서 pointerDown 후 월드로 drag  
When: pointerUp  
Then: UI 소비; world command0  
Result: NOT_RUN

**TEST-CS-PLAY.01-N**

Given: IME 중 Space/WASD  
When: pointerUp  
Then: GAME_ACTION0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PLAY/Play01Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPLAY01PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PLAY.01.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-008, REQ-072.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-PLAY.02 · 팀·업무 선택과 명령 미리보기

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SelectionSet→CommandIntent→PreviewResult→CommandReceipt; UI 수락은 업무 완료가 아님.

### 필수 상세 명세
- [06-native-surfaces](specs/06-native-surfaces.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Selection/SelectionService.cs`
- `Assets/ChooGuard/Presentation/Commands/CommandPreviewPresenter.cs`
- `Assets/ChooGuard/Presentation/Commands/CommandPreview.prefab`

### 선행 산출물과 소비 단계
- `CS-PLAY.01:candidate` → `CS-PLAY.02:integration` / 조건 `ALWAYS`.
- `CS-OPS.01:candidate` → `CS-PLAY.02:integration` / 조건 `ALWAYS`.
- `CS-OPS.02:candidate` → `CS-PLAY.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 팀·승무원 연결 차량을 기본 단위로 하고 목록/월드 선택을 같은 SelectionSet으로 연결한다.
2. 혼합기관 선택은 대상별 조건·요청/거부를 표시한다. 한 업무의 원자성 경계와 독립 업무의 부분 성공을 구분한다.
3. Submit은 명시적인 확인 뒤 수행한다. 테스트 double UI 완료와 실물 커널 통합 완료를 별도로 기록한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PLAY.02-P**

Given: teamA valid/teamB denied  
When: preview 뒤 제출  
Then: 대상별 결과; 한 업무 원자성  
Result: NOT_RUN

**TEST-CS-PLAY.02-N**

Given: preview 후 state revision 변화  
When: preview 뒤 제출  
Then: STALE_STATE_REPREVIEW  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PLAY/Play02Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPLAY02PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PLAY.02.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-008, REQ-009, REQ-010.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: GAME-EMERGENCY, GAME-RESCUEHQ. [출처 등록부](reference/sources.json).

---

## CS-PLAY.03 · 게임 HUD·카메라·미니맵

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** UIState {selection,camera,openPanels,textScale}; 운영 상태와 수명을 분리.

### 필수 상세 명세
- [06-native-surfaces](specs/06-native-surfaces.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Hud/OperationsHud.prefab`
- `Assets/ChooGuard/Presentation/Hud/HudPresenter.cs`
- `Assets/ChooGuard/Presentation/Camera/OperationsCamera.cs`
- `Assets/ChooGuard/Presentation/Hud/MinimapPresenter.cs`

### 선행 산출물과 소비 단계
- `CS-PLAY.01:candidate` → `CS-PLAY.03:integration` / 조건 `ALWAYS`.
- `CS-WORLD.03:candidate` → `CS-PLAY.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. WorldCamera를 중앙으로 하고 접이식 조직 목록·inspector·command dock·timeline을 overlay canvas로 조립한다.
2. 미니맵은 실제 위치를 투영하거나 별도 camera를 사용한다. 클릭 변환은 실제 pixelRect·층·DPI를 반영한다.
3. 위치 변경 애니메이션은 receipt/물리 결과를 따라가며 업무 완료나 실제 도착의 근거가 되지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PLAY.03-P**

Given: 실제 world marker와 다른 층 대상  
When: 카메라/층 조작  
Then: ID anchor 일치; 미니맵 실제좌표  
Result: NOT_RUN

**TEST-CS-PLAY.03-N**

Given: behind-camera 좌표  
When: 카메라/층 조작  
Then: 라벨 숨김; domain 불변  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PLAY/Play03Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPLAY03PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PLAY.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-008, REQ-072.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-PLAY.04 · 복수 원인·기관별 정보·작업 오버레이

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ReasonView/AgencyTimeline는 read-only; pause는 코어 ack 이후 표시.

### 필수 상세 명세
- [06-native-surfaces](specs/06-native-surfaces.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Inspector/ReasonInspector.cs`
- `Assets/ChooGuard/Presentation/Timeline/AgencyTimeline.cs`
- `Assets/ChooGuard/Presentation/Overlays/ToolOverlayRouter.cs`
- `Assets/ChooGuard/Presentation/Inspector/ReasonInspector.prefab`

### 선행 산출물과 소비 단계
- `CS-PLAY.02:candidate` → `CS-PLAY.04:integration` / 조건 `ALWAYS`.
- `CS-OPS.06:candidate` → `CS-PLAY.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 각 reason에 원인·영향·관련 업무/보고·해결 조건·근거를 붙인다.
2. 전체 분석에서 기관 정보 보기로 전환해도 도메인 knowledge는 읽기만 한다.
3. 모달/도구 종료시 focus·selection·camera를 복원한다. 열린 창과 session pause는 다른 상태다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PLAY.04-P**

Given: task 원인2개와 원문근거  
When: inspector 열기/닫기  
Then: 복수원인/영향/focus 보존  
Result: NOT_RUN

**TEST-CS-PLAY.04-N**

Given: author 분석을 agency 지식으로 쓰기  
When: inspector 열기/닫기  
Then: READ_SCOPE_VIOLATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PLAY/Play04Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPLAY04PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PLAY.04.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-008, REQ-072.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-PLAY.05 · 접근성·재배정·텍스트와 반복 조작 효율

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.05 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Preferences는 물리·정보·업무 결과를 수정하지 않는다.

### 필수 상세 명세
- [06-native-surfaces](specs/06-native-surfaces.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Accessibility/UiPreferences.cs`
- `Assets/ChooGuard/Presentation/Accessibility/FocusManager.cs`
- `Assets/ChooGuard/Presentation/Menu/WorkspaceLauncher.cs`
- `Assets/ChooGuard/Presentation/Menu/WorkspaceLauncher.prefab`
- `Assets/ChooGuard/Presentation/Accessibility/Preferences.prefab`

### 선행 산출물과 소비 단계
- `CS-PLAY.03:candidate` → `CS-PLAY.05:integration` / 조건 `ALWAYS`.
- `CS-PLAY.04:candidate` → `CS-PLAY.05:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 1920x1080 기준에서 창 크기·100/150/200% 텍스트·모달 포커스 순서를 검사한다.
2. World 정밀 클릭 없이 목록/업무 패널만으로 배정·취소·비교·근거 조회가 가능하게 한다.
3. 지원한 접근성 기능만 표시한다. DOM/웹 시험이나 Editor 성공을 Player 적합성으로 사용하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PLAY.05-P**

Given: 1280폭·글자200%·keyboard only  
When: 핵심 과제  
Then: 클리핑 없이 목록 배정 가능  
Result: NOT_RUN

**TEST-CS-PLAY.05-N**

Given: 글자를 자동축소해 확대 무력화  
When: 핵심 과제  
Then: ACCESSIBILITY_REJECT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PLAY/Play05Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPLAY05PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PLAY.05.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-072.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-PLAY.06 · 체크포인트·분기·A/B 비교 네이티브 화면

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.06 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** CheckpointCapabilities→BranchRequest; ComparisonReport→read-only UI; 기록 replay와 새 branch·운영안 수정 구분.

### 필수 상세 명세
- [06-native-surfaces](specs/06-native-surfaces.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Experiments/BranchDialog.cs`
- `Assets/ChooGuard/Presentation/Experiments/ComparisonPresenter.cs`
- `Assets/ChooGuard/Presentation/Experiments/BranchDialog.prefab`
- `Assets/ChooGuard/Presentation/Experiments/ComparisonOverlay.prefab`

### 선행 산출물과 소비 단계
- `CS-PLAY.05:candidate` → `CS-PLAY.06:integration` / 조건 `ALWAYS`.
- `CS-LAB.03:candidate` → `CS-PLAY.06:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 분기 후보의 복원 가능성·원본·시각·누락 워커를 표시하고 허용된 요청만 명시 확인 후 전송한다.
2. 비교 전 inputBasis의 일치 여부와 공통 QoI를 표시하며 비교불가·실패·우열 불명을 정상적인 결과로 보인다.
3. 타임라인·공간 카메라를 연동하되 미래 정보를 기관 지식에 쓰지 않는다. 선택 이유를 받아 대본 편집으로 전달한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PLAY.06-P**

Given: A snapshot 완전/B 다른계획  
When: 분기→비교  
Then: 부모불변; B 별도 run; 근거와 선택이유  
Result: NOT_RUN

**TEST-CS-PLAY.06-N**

Given: worker 빠진 checkpoint  
When: 분기→비교  
Then: EXACT_FORK_DISABLED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PLAY/CSPLAY06Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPLAY06PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PLAY.06.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-008, REQ-072.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-PLAY.07 · 동일 코어의 연구용 표·타임라인 비교 인터페이스

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.07 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** StudyConfiguration {condition, coreLock, assistancePolicy, informationScope, caseId}; 연구 전용 condition이며 사용자 모드가 아님.

### 필수 상세 명세
- [06-native-surfaces](specs/06-native-surfaces.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)
- [10-observability-and-study](specs/10-observability-and-study.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Research/TabularStudyPresenter.cs`
- `Assets/ChooGuard/Presentation/Research/TabularStudy.prefab`
- `Assets/ChooGuard/Contracts/StudyConfiguration.cs`

### 선행 산출물과 소비 단계
- `CS-PLAY.05:candidate` → `CS-PLAY.07:integration` / 조건 `ALWAYS`.
- `CS-OPS.07:candidate` → `CS-PLAY.07:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 연구 빌드에서만 B/C 조건을 제시하고 동일 OperationsPort·로그·규칙·AI·도움 정책을 사용한다. B에는 3D만 감춘다.
2. B에서 배정·취소·원인 조회·분기·비교·대본이라는 동일 과업을 수행할 수 있도록 표 기반 경로를 제공한다.
3. 배포 메뉴에는 두 사용자 모드만 유지한다. 연구 조건과 실행순서를 기록하며 B에 다른 계산이나 축소된 정보를 주지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PLAY.07-P**

Given: 같은 core/model/info/AI lock의 B/C  
When: 같은 과제  
Then: 3D 외 기능·정보 parity  
Result: NOT_RUN

**TEST-CS-PLAY.07-N**

Given: B만 다른 업무 timer  
When: 같은 과제  
Then: STUDY_PARITY_FAIL  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PLAY/CSPLAY07Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPLAY07PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PLAY.07.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-081.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).


# CS-LAB · 저장된 운영안의 분기·비교 실험

## CS-LAB.01 · 이벤트 재생·완전 checkpoint

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Checkpoint: schemas/Checkpoint.schema.json. cutSequence/tickUs/coreStateRef/eventQueueRef/randomStreamsRef/contentLockRef와 필수 workerStates를 검증한다.

### 필수 상세 명세
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/CheckpointService.cs`
- `Assets/ChooGuard/Experiments/ReplayReader.cs`
- `Assets/ChooGuard/Experiments/CheckpointManifest.cs`

### 선행 산출물과 소비 단계
- `CS-OPS.02:candidate` → `CS-LAB.01:integration` / 조건 `ALWAYS`.
- `CS-OPS.05:candidate` → `CS-LAB.01:integration` / 조건 `ALWAYS`.
- `CS-SIM.01:candidate` → `CS-LAB.01:integration` / 조건 `EXTERNAL_WORKER_USED`.

### 구현 절차
1. 공통 cut에서 업무·예약·기관지식·미전달 메시지·난수·worker 상태 또는 재계산 recipe를 모은다.
2. 필수 worker 목록은 capability와 run config에서 구한다. 파일 이름 존재만으로 정확 복원을 허용하지 않는다.
3. hash·version·시간을 검증한 후 manifest를 durable로 게시한다. 누락되면 복구 제한을 명시한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.01-P**

Given: required w1,w2; 동일 cut7/tick20  
When: checkpoint 검사  
Then: 동일 worker집합·시간·해시  
Result: NOT_RUN

**TEST-CS-LAB.01-N**

Given: w2 누락 또는 w1 중복  
When: checkpoint 검사  
Then: INCOMPLETE_CHECKPOINT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-036.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-LAB.02 · 원본 불변 분기·재실행

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** BranchReceipt {newRunId,parentRunId,checkpointHash,kind,changedPlan}; 리플레이는 새 실험이 아니다.

### 필수 상세 명세
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/BranchService.cs`
- `Assets/ChooGuard/Experiments/BranchLineage.cs`

### 선행 산출물과 소비 단계
- `CS-LAB.01:candidate` → `CS-LAB.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 새 runId와 parent checkpoint reference를 만들고 원래 run을 갱신하지 않는다.
2. 지원하는 정확 복원과 새 초기화 실행을 구별한다. user note는 과거 원래 의도로 덮어쓰지 않는다.
3. 불완전 복원 capability이면 특정 시점 branch를 비활성화하고 다른 시작점을 명시한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.02-P**

Given: parent run-a/cut7  
When: branch b  
Then: parent bytes 불변; child generation 신규  
Result: NOT_RUN

**TEST-CS-LAB.02-N**

Given: parent의 late RESULT  
When: branch b  
Then: FOREIGN_GENERATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab02Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-037, REQ-057.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-LAB.03 · 동일조건·불확도·비지배 비교

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Comparison {basis,inputs,qoi,violations,uncertainty,status,selectionReason}; 현실 전체 최적을 주장하지 않음.

### 필수 상세 명세
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/ComparisonService.cs`
- `Assets/ChooGuard/Experiments/ExogenousScenarioStreams.cs`
- `Assets/ChooGuard/Experiments/ComparisonReport.cs`

### 선행 산출물과 소비 단계
- `CS-LAB.02:candidate` → `CS-LAB.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. map/rule/model/QoI basis가 호환되는지 먼저 검사한다. 외생 난수 혁신은 stable process key로 결속한다.
2. 사용자 조치가 바꾸는 후속 이동·보고·혼잡은 각 run에서 다시 계산한다.
3. 제약 위반을 속도 점수로 상쇄하지 않는다. 불확도·실패·우열 불명과 다목적 비지배 대안을 표시한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.03-P**

Given: 동일 exogenous process key, 다른 운영안  
When: A/B 비교  
Then: 외생 혁신 동일·내생값 재계산  
Result: NOT_RUN

**TEST-CS-LAB.03-N**

Given: modelLock 다른 두 run  
When: A/B 비교  
Then: INCOMPARABLE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab03Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-038, REQ-039.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-LAB.04 · 변경 영향·캐시·부분 재실행

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-LAB.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Impact {changedInputs,dirtyOutputs,evidence,allowedReuse,requiredRecompute}; 관계 부재는 영향 없음이 아님.

### 필수 상세 명세
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [03-durable-operations](specs/03-durable-operations.md)

### 새 구현 경로
- `Assets/ChooGuard/Experiments/ImpactGraph.cs`
- `Assets/ChooGuard/Experiments/ReplayCache.cs`
- `Assets/ChooGuard/Experiments/RunInvalidation.cs`

### 선행 산출물과 소비 단계
- `CS-LAB.03:candidate` → `CS-LAB.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 표현/서식 변경과 의미/물리 입력 변경을 구분한다.
2. 입력·버전·설정 hash와 영향 경계를 입증할 수 있을 때만 파생결과를 재사용한다.
3. 영향이 누락됐거나 비선형 파급을 제한할 수 없으면 확대 재계산한다. 과거 증거를 삭제하지 않고 현재 사용 자격을 STALE로 둔다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-LAB.04-P**

Given: 의미입력 하나 변경  
When: 부분/전체 재실행 대조  
Then: 동일 허용오차내 결과  
Result: NOT_RUN

**TEST-CS-LAB.04-N**

Given: 영향 graph 불완전  
When: 부분/전체 재실행 대조  
Then: FULL_RECOMPUTE_REQUIRED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/LAB/Lab04Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-084.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).


# CS-MODES · 매뉴얼 교육과 제약 기반 랜덤 실험

## CS-MODES.01 · 한 개의 근거 기반 교육 과정

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-MODES.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TutorialCourse {source,scope,goals,partialOrder,hints,assessment}; 공식 이수·훈련 정확재현은 별도 검수.

### 필수 상세 명세
- [08-modes-and-scenario-generation](specs/08-modes-and-scenario-generation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)

### 새 구현 경로
- `Assets/ChooGuard/Scenarios/TutorialDirector.cs`
- `content/exercises/tutorial/course.json`
- `Assets/ChooGuard/Presentation/Tutorial/TutorialOverlay.prefab`
- `Assets/ChooGuard/Presentation/Tutorial/TutorialPresenter.cs`

### 선행 산출물과 소비 단계
- `CS-PACK.02:candidate` → `CS-MODES.01:integration` / 조건 `ALWAYS`.
- `CS-OPS.05:candidate` → `CS-MODES.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 실제 사례를 재구성한 부분과 원 대본이 확인된 부분을 구분한다.
2. 평가는 부분순서·조건·증거를 사용하고 독립 업무의 유효한 순서 변경을 허용한다.
3. 힌트는 표시만 바꾸며 물리·기관권한·자원능력·전달시간을 수정하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-MODES.01-P**

Given: 독립 taskA/taskB 목표  
When: 순서 AB와 BA  
Then: 둘 다 학습통과  
Result: NOT_RUN

**TEST-CS-MODES.01-N**

Given: hint가 speed 변경  
When: 순서 AB와 BA  
Then: CORE_PARITY_FAIL  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/MODES/Modes01Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-003, REQ-004, REQ-065.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: CASE-READY2026, CASE-YULHYEON, MAN-SOP. [출처 등록부](reference/sources.json).

---

## CS-MODES.02 · 제약 기반 랜덤 상황

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-MODES.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** GeneratedScenario {spec,seedManifest,provenance,constraintReport,qualification}; 무작위 발생확률은 현실 빈도와 별개.

### 필수 상세 명세
- [08-modes-and-scenario-generation](specs/08-modes-and-scenario-generation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)

### 새 구현 경로
- `Assets/ChooGuard/Scenarios/ScenarioGenerator.cs`
- `Assets/ChooGuard/Scenarios/ScenarioConstraintChecker.cs`
- `content/exercises/random/grammar.json`

### 선행 산출물과 소비 단계
- `CS-PACK.01:candidate` → `CS-MODES.02:integration` / 조건 `ALWAYS`.
- `CS-OPS.06:candidate` → `CS-MODES.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 기관·공간·자원과 검토된 사건 문법을 입력으로 받고 조건부로 사건을 표본화한다.
2. 성공/거부 분포와 seed·각 난수 혁신·거부 이유를 보존한다. 반복상한에서 무한생성 대신 조건 조정을 요구한다.
3. FEASIBLE·ESCALATION_REQUIRED·CONFLICTED_INPUT·UNSUPPORTED_DOMAIN을 구분한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-MODES.02-P**

Given: 고정 grammar와 동일 seed manifest  
When: 제약상황 생성  
Then: 같은 spec; rejection 추적  
Result: NOT_RUN

**TEST-CS-MODES.02-N**

Given: 64회 연속 조건실패  
When: 제약상황 생성  
Then: GENERATION_EXHAUSTED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/MODES/Modes02Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-005, REQ-006, REQ-007.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-MODES.03 · 현장 재사용·모드 연결·다음 판단 지점

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-MODES.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Mode={TUTORIAL,RANDOM_OPERATIONS_LAB}; RuntimeConfig는 같은 코어와 lock을 공유.

### 필수 상세 명세
- [08-modes-and-scenario-generation](specs/08-modes-and-scenario-generation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)

### 새 구현 경로
- `Assets/ChooGuard/Scenarios/WorkspaceSessionFactory.cs`
- `Assets/ChooGuard/Scenarios/DecisionBoundaryRunner.cs`
- `Assets/ChooGuard/Scenarios/ModePolicy.cs`
- `Assets/ChooGuard/Presentation/Scenarios/ScenarioSetupPresenter.cs`
- `Assets/ChooGuard/Presentation/Scenarios/SiteSelectionPresenter.cs`
- `Assets/ChooGuard/Presentation/Scenarios/ScenarioSetup.prefab`
- `Assets/ChooGuard/Presentation/Scenarios/SiteSelection.prefab`

### 선행 산출물과 소비 단계
- `CS-MODES.01:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-MODES.02:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-LAB.02:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-PLAY.05:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.
- `CS-SIM.01:candidate` → `CS-MODES.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 현장·규칙·template·model 묶음을 읽고 새 운영안을 만든다. 원본 번들은 바꾸지 않는다.
2. 제품 모드는 두 개만 허용하고 analysis/branch/solver profile은 공통 도구로 둔다.
3. 다음 판단지점 이동은 사건과 solver를 순차 처리한다. 미래 결과를 예측 없이 미리 게시하거나 dt를 늘리지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-MODES.03-P**

Given: 다음 의사결정 이벤트 tick100  
When: 진행요청  
Then: 순차계산 후100; 미래정보 노출0  
Result: NOT_RUN

**TEST-CS-MODES.03-N**

Given: worker 미준비  
When: 진행요청  
Then: COMPUTING_NOT_FAST_FORWARD  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/MODES/Modes03Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSMODES03PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-MODES.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-002, REQ-083, REQ-085.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).


# CS-SIM · 다중 물리·행동 계산과 정량 검증

## CS-SIM.01 · 새 worker 프로토콜·시험 워커

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** WorkerCapability, SimulationJob, FieldBatch; 임의 shell 문자열을 입력으로 실행하지 않는다.

### 필수 상세 명세
- [05-worker-and-cosimulation](specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Contracts/WorkerTypes.cs`
- `Assets/ChooGuard/Simulation/WorkerProcessClient.cs`
- `workers/fixture/worker.py`
- `workers/protocol/worker.schema.json`

### 선행 산출물과 소비 단계
- `CS-BOOT.02:candidate` → `CS-SIM.01:integration` / 조건 `ALWAYS`.
- `CS-PACK.01:candidate` → `CS-SIM.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 로컬 child process 표준입출력에 UTF-8 JSONL을 사용한다. 1줄 최대 1 MiB, 깊이 32, inline payload 64 KiB, 큰 장은 allowlisted relative blobRef로 전달한다. stdout은 protocol 전용, stderr는 bounded log다. 프로세스는 검수된 executable·argv만 실행한다.
2. capability에 현상·단위·time-step·batch/interactive·checkpoint·검증영역을 선언한다.
3. worker timeout/exit/corrupt/late result/다른 branch 응답을 검사한다. 시험 워커는 SYNTHETIC_FIXTURE이며 실제 모델로 승격하지 않는다.
4. requestId·jobId·attemptId·runId·generation·inputDigest를 확인하고 다음 correlation이 없는 결과는 격리한다. 취소 후 generation이 이전인 결과는 publish하지 않는다.
5. worker handle은 OS process 종료·timeout과 메모리/출력 한도를 검사한다. 이런 제한은 sandbox 보장을 뜻하지 않으므로 임의 내려받은 코드를 실행하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.01-P**

Given: JSONL 1줄+정상 correlation  
When: worker 결과 수신  
Then: job/run/generation/input 일치  
Result: NOT_RUN

**TEST-CS-SIM.01-N**

Given: 1MiB 초과 또는 duplicate key  
When: worker 결과 수신  
Then: PROTOCOL_REJECT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim01Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.01.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-049.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: AUD-FMI. [출처 등록부](reference/sources.json).

---

## CS-SIM.02 · 보행·층간 연결 어댑터

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** PedestrianState {entityId,frameId,position,velocity,tick,modelRef}; 독립 관측 전에는 현장 정확도 미판정.

### 필수 상세 명세
- [05-worker-and-cosimulation](specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/PedestrianAdapter.cs`
- `workers/pedestrian/runner.py`
- `benchmarks/pedestrian/protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.02:integration` / 조건 `ALWAYS`.
- `CS-WORLD.01:candidate` → `CS-SIM.02:integration` / 조건 `ALWAYS`.
- `CS-WORLD.02:candidate` → `CS-SIM.02:qualification` / 조건 `NAMED_SITE_ACCURACY`.

### 구현 절차
1. JuPedSim 등의 기준·후보를 동일 geometry·독립 현상 데이터로 비교한다.
2. 위치의 정본은 한 엔진에만 둔다. Unity의 표시·회피 로직과 이중 소유하지 않는다.
3. 계단/승강기/보조 이동은 지원 범위를 별도 기술하고 기본 보행모델의 정확도를 자동 상속하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.02-P**

Given: 동일 geometry,독립 pedestrian 관측  
When: adapter 비교  
Then: 위치 owner1; holdout별 오차  
Result: NOT_RUN

**TEST-CS-SIM.02-N**

Given: Unity 별도 위치갱신  
When: adapter 비교  
Then: DUPLICATE_FIELD_OWNER  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim02Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.02.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-019, REQ-043.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-JPS14, TECH-JUPED, TECH-PEDDATA. [출처 등록부](reference/sources.json).

---

## CS-SIM.03 · 접근교통·차량 운행 어댑터

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TrafficState {vehicleId,crewIds,routeId,frameId,tick,provenance}; 철도 운행은 충돌/탈선 동역학과 다름.

### 필수 상세 명세
- [05-worker-and-cosimulation](specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/TrafficAdapter.cs`
- `workers/traffic/runner.py`
- `benchmarks/traffic/protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. SUMO 등 후보에 현지 경로·가용성·차량·운행조건을 결속한다.
2. 정량 프로파일에서는 teleport·숨은 우회 등을 탐지하고 해당 실행의 자격을 제한한다.
3. 해당 QoI가 필요 없는 과제는 명시적 도착 시나리오 입력을 쓸 수 있지만 실제 출동시간 예측으로 부르지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.03-P**

Given: 입력 route/model lock  
When: traffic 실행  
Then: 사례범위 및 shortcut 기록  
Result: NOT_RUN

**TEST-CS-SIM.03-N**

Given: teleport 사용 실행을 정상예측 표시  
When: traffic 실행  
Then: UNQUALIFIED_TRAFFIC_RESULT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim03Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-044.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-SUMO, TECH-SUMO-RAIL. [출처 등록부](reference/sources.json).

---

## CS-SIM.04 · 화재·열·연기 기준 계산

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** HazardRun {inputHash,solverLock,boundaryHistory,grid,qoi,resultRefs,validation}; 실제 관측없는 기준계산 일치는 현실 검증이 아님.

### 필수 상세 명세
- [05-worker-and-cosimulation](specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/HazardReferenceAdapter.cs`
- `workers/hazard/reference_runner.py`
- `benchmarks/hazard/reference-protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.04:integration` / 조건 `ALWAYS`.
- `CS-WORLD.02:candidate` → `CS-SIM.04:qualification` / 조건 `NAMED_SITE_ACCURACY`.

### 구현 절차
1. 고정된 FDS 실행 버전과 물성·형상·개구·열원·경계·관측을 검사한다.
2. 기본 경로는 batch reference이며 live rollback을 지원한다고 가정하지 않는다. 수렴·독립 데이터·QoI를 별도 평가한다.
3. 필요 기준을 실행 전에 고정한다. 영상 전환·VFX를 온도/농도 데이터로 사용하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.04-P**

Given: 고정 grid/material/history  
When: reference 계산  
Then: QoI와 수렴/관측 역할 분리  
Result: NOT_RUN

**TEST-CS-SIM.04-N**

Given: FDS 결과만으로 현장검증 선언  
When: reference 계산  
Then: NO_INDEPENDENT_OBSERVATION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim04Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.04.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-042.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: TECH-FDS, TECH-FDS-SCOPE. [출처 등록부](reference/sources.json).

---

## CS-SIM.05 · 공동시간·불확도·가속·정직한 복구

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SIM.05 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** CommittedBoundary {tick,sequence,inputs,fieldOwners,resultHashes,capabilities}; 독립 reference/모델 결과는 runtime schema와 별도로 qualification.

### 필수 상세 명세
- [05-worker-and-cosimulation](specs/05-worker-and-cosimulation.md)
- [04-checkpoint-and-comparison](specs/04-checkpoint-and-comparison.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Simulation/CosimulationCoordinator.cs`
- `Assets/ChooGuard/Simulation/QuantityOwnership.cs`
- `Assets/ChooGuard/Simulation/ModelQualification.cs`
- `workers/hazard/surrogate_runner.py`
- `benchmarks/coupling/protocol.json`

### 선행 산출물과 소비 단계
- `CS-SIM.01:candidate` → `CS-SIM.05:integration` / 조건 `ALWAYS`.
- `CS-OPS.05:candidate` → `CS-SIM.05:integration` / 조건 `ALWAYS`.
- `CS-LAB.01:candidate` → `CS-SIM.05:integration` / 조건 `ALWAYS`.
- `CS-SIM.02:candidate` → `CS-SIM.05:integration` / 조건 `PEDESTRIAN_USED`.
- `CS-SIM.03:candidate` → `CS-SIM.05:integration` / 조건 `TRAFFIC_USED`.
- `CS-SIM.04:candidate` → `CS-SIM.05:integration` / 조건 `HAZARD_USED`.

### 구현 절차
1. run에서 사용하는 worker만 참여시켜 시간·단위·좌표·수량별 writer와 불연속 사건 동기화를 검사한다.
2. required worker 결과가 실패하면 경계를 commit하지 않는다. 복원이 안 되면 공통 유효 checkpoint/시작 상태부터 재계산한다.
3. 선택적 ROM/PhysicsNeMo 후보는 기준·관측·독립 형상에서 장기 roll-out·보존량·OOD를 검사한다. 가속모델이 없다는 이유로 필수 reference 경로를 삭제하지 않는다.
4. 훈련/비교에 쓰는 QoI가 요구하는 현상만 설치하되, 중요한 물리를 제외하고 정확도 주장을 유지하지 않는다.
5. UI 배속과 독립인 정수 microsecond tick으로 교환 경계를 만든다. 각 참여 solver의 안정 step과 valid interval을 유지하며 fresh barrier를 모두 통과한 경계만 원자 게시한다.
6. 문 변경 후 이전 inputDigest의 solver 결과는 거부한다. 실패시 모든 영향 worker를 마지막 공통 cut에 복원하거나 재실행하며 일부 전진 상태를 current로 쓰지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SIM.05-P**

Given: 두 worker 동일 경계 ready  
When: co-sim publish  
Then: single committed boundary  
Result: NOT_RUN

**TEST-CS-SIM.05-N**

Given: worker1전진,worker2실패  
When: co-sim publish  
Then: NO_PARTIAL_PUBLISH; 공통cut 복원  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SIM/Sim05Tests.cs` · NOT_RUN
- WORKER_INTEGRATION: `benchmarks/tests/CS-SIM.05.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-035, REQ-046, REQ-047, REQ-048, REQ-049.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: STD-FMI, TECH-MAPANYTHING, TECH-PHYSICSNEMO. [출처 등록부](reference/sources.json).


# CS-SCRIPT · 선택 운영안의 근거 기반 대본 저작

## CS-SCRIPT.01 · 허용 근거 조회와 규칙/설명 후보

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** AssistantProposal {type,claims,evidenceRefs,missing,patchDraft}; 결정적 상태 계산은 코어 담당.

### 필수 상세 명세
- [09-script-and-approval](specs/09-script-and-approval.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `Assets/ChooGuard/Authoring/EvidenceQuery.cs`
- `Assets/ChooGuard/Authoring/AssistantGateway.cs`
- `workers/authoring/retrieve.py`
- `workers/authoring/contracts.json`

### 선행 산출물과 소비 단계
- `CS-PACK.02:candidate` → `CS-SCRIPT.01:integration` / 조건 `ALWAYS`.
- `CS-OPS.06:candidate` → `CS-SCRIPT.01:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 기관·사건·판본·권한으로 문맥을 좁히고 실제 source locator를 연결한다.
2. AI는 규칙/설명/수정안 후보만 반환한다. 승인·DB쓰기·shell·운영명령 권한은 부여하지 않는다.
3. 문서/메모의 지시문을 데이터로 다루고 비용·timeout·반출 policy를 검사한다. 실패시 수동편집은 계속한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SCRIPT.01-P**

Given: 허용된 clause/run 근거  
When: AI proposal  
Then: 기록·가설·제안 분리  
Result: NOT_RUN

**TEST-CS-SCRIPT.01-N**

Given: 원문에서 shell 지시  
When: AI proposal  
Then: UNTRUSTED_INSTRUCTION  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SCRIPT/Script01Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSSCRIPT01PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-SCRIPT.01.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-032, REQ-052, REQ-058, REQ-059.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-SCRIPT.02 · 조건부 운영안·ScriptIR

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ScriptIR {revision,plan,scenario,steps,roles,conditions,evidence,qualifications}; 내용변경은 새 revision.

### 필수 상세 명세
- [09-script-and-approval](specs/09-script-and-approval.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `Assets/ChooGuard/Authoring/ScriptCompiler.cs`
- `Assets/ChooGuard/Authoring/ScriptIR.cs`
- `Assets/ChooGuard/Authoring/ScriptSemanticValidator.cs`

### 선행 산출물과 소비 단계
- `CS-SCRIPT.01:candidate` → `CS-SCRIPT.02:integration` / 조건 `ALWAYS`.
- `CS-LAB.02:candidate` → `CS-SCRIPT.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. 사실·사후 설명·가설·매뉴얼 근거 제안·검수된 지시를 단계별로 구분한다.
2. 실제 완료시각을 영구적인 정답 시각으로 바꾸지 않고 역할·선행조건·확인·예외로 표현한다.
3. 동일 ScriptIR에서 대본과 실행 데이터를 만들며 존재하지 않는 entity/rule/role을 거부한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SCRIPT.02-P**

Given: plan/scenario와 실제 event refs  
When: ScriptIR 생성  
Then: 역할/조건/분기 동등  
Result: NOT_RUN

**TEST-CS-SCRIPT.02-N**

Given: 미존재 event로 사실 주장  
When: ScriptIR 생성  
Then: MISSING_EVIDENCE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SCRIPT/Script02Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSSCRIPT02PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-SCRIPT.02.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-051, REQ-053, REQ-054.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: REF-MSEL. [출처 등록부](reference/sources.json).

---

## CS-SCRIPT.03 · native 대본 편집·검토 상태

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ReviewDecision {reviewer,role,scope,revision,evidence,time,status}; focus와 UI가 승인권을 부여하지 않음.

### 필수 상세 명세
- [09-script-and-approval](specs/09-script-and-approval.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `Assets/ChooGuard/Presentation/Authoring/ScriptEditorPresenter.cs`
- `Assets/ChooGuard/Presentation/Authoring/ScriptEditor.prefab`
- `Assets/ChooGuard/Authoring/ReviewLedger.cs`
- `Assets/ChooGuard/Authoring/QualificationPolicy.cs`

### 선행 산출물과 소비 단계
- `CS-SCRIPT.02:candidate` → `CS-SCRIPT.03:integration` / 조건 `ALWAYS`.
- `CS-PLAY.05:candidate` → `CS-SCRIPT.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. native overlay에서 입력·선택·커서·IME를 유지하고 문장을 누르면 실제 근거를 연다.
2. 서식 변경과 의미 변경을 분리하고 의미 변경은 관련 검토를 만료시킨다.
3. 초안/시뮬레이션사용 검토/기관의 지정훈련 수용은 다른 주체·범위·버전 기록이다. 단어만으로 승인되지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SCRIPT.03-P**

Given: approved script rev2  
When: 의미 수정 rev3  
Then: approval stale; raw log 불변  
Result: NOT_RUN

**TEST-CS-SCRIPT.03-N**

Given: AI 문구 승인됨  
When: 의미 수정 rev3  
Then: 승인 상태 변화0  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SCRIPT/Script03Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSSCRIPT03PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-SCRIPT.03.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-055, REQ-056.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-SCRIPT.04 · 고객 양식 출력·재로드·수정 회수

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TemplateProfile {format,revision,fields,exporter,qualification}; 결과는 임의 script를 실행하지 않는다.

### 필수 상세 명세
- [09-script-and-approval](specs/09-script-and-approval.md)
- [02-wire-and-ports](specs/02-wire-and-ports.md)

### 새 구현 경로
- `Assets/ChooGuard/Authoring/ExportService.cs`
- `Assets/ChooGuard/Authoring/ImportReconciliation.cs`
- `workers/authoring/export_document.py`
- `content/templates/profiles.json`
- `Assets/ChooGuard/Presentation/Authoring/ExportDialog.prefab`
- `Assets/ChooGuard/Presentation/Authoring/ExportDialogPresenter.cs`

### 선행 산출물과 소비 단계
- `CS-SCRIPT.03:candidate` → `CS-SCRIPT.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. JSON·Markdown·DOCX를 공통 IR에서 출력한다. 기관별 다른 형식은 실제 템플릿의 입력/출력 검증 후 활성화한다.
2. 출력 문서와 실행형 구조의 역할·사건·분기·조건을 비교한다.
3. 외부 편집본 의미diff는 수동 또는 지원한 importer로 조정한다. 자동 왕복이 안 되는 형식을 가능한 것으로 표시하지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SCRIPT.04-P**

Given: 유효 template/ScriptIR  
When: 출력·의미 재로드  
Then: 조건/역할/근거 동등  
Result: NOT_RUN

**TEST-CS-SCRIPT.04-N**

Given: 미지원 HWPX를 지원 주장  
When: 출력·의미 재로드  
Then: UNSUPPORTED_TEMPLATE  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SCRIPT/Script04Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSSCRIPT04PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-SCRIPT.04.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-086.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-05, DISC-07. [출처 등록부](reference/sources.json).


# CS-PROOF · 사용자 가치·현장 적합성 검증

## CS-PROOF.01 · 실제 작성팀·자료·시간 가설 조사

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** StudyIntake {consent,roles,caseRefs,workflow,template,measurements,missing}; 표본 제안과 모집 완료를 분리.

### 필수 상세 명세
- [10-observability-and-study](specs/10-observability-and-study.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `research/discovery/protocol.md`
- `research/discovery/intake.schema.json`
- `research/discovery/time-observation.schema.json`

### 선행 산출물과 소비 단계
선행 구현 산출물 없음. 현재 작업의 입력과 안전 경계는 확인한다.

### 구현 절차
1. 반복 작성자·기관 협조자·검수자·자료담당을 구분하고 최근 대본/수정/양식을 허용범위에서 관찰한다.
2. 교육·대리입력·기관회신·무인계산·사람 작업량을 분리한다. 동일인 중첩시간은 합집합이다.
3. 자료/인터뷰 미확보는 해당 고객 실증만 제한한다. 합성 코어 구현을 막는 전역 선행이 아니다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.01-P**

Given: 동의된 실제 과제  
When: 최근 업무관찰  
Then: 가명/누락/도움 표시  
Result: NOT_RUN

**TEST-CS-PROOF.01-N**

Given: 가상의 인터뷰 답  
When: 최근 업무관찰  
Then: NOT_OBSERVED  
Result: NOT_RUN

### 실제 시험 매체
- DOCUMENT_REVIEW: `research/proof/01-evaluation-protocol.md` · NOT_RUN

### 연결과 인계
제품 요구: REQ-077, REQ-078.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-01, DISC-03, DISC-05, DISC-18. [출처 등록부](reference/sources.json).

---

## CS-PROOF.02 · 종단 작동·장애·성능 검수

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Qualification별 evidence bundle; 기능/성능/정량/현장 수용은 별도.

### 필수 상세 명세
- [10-observability-and-study](specs/10-observability-and-study.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Tests/Acceptance/EndToEndFlowTests.cs`
- `Assets/ChooGuard/Tests/Acceptance/FaultInjectionTests.cs`
- `qualification/technical-profile.json`

### 선행 산출물과 소비 단계
- `CS-OPS.06:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-PLAY.05:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-LAB.04:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-MODES.03:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-SCRIPT.04:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-PLAY.06:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.
- `CS-OPS.07:candidate` → `CS-PROOF.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. A안 문제발견→B수정→비교→대본→새 run을 실제 Player에서 끝까지 수행한다.
2. 저장실패·worker장애·로그손상·중복명령·UI뒤클릭·IME·network loss를 반례로 만든다.
3. 고정 부하에서 UI/렌더/운영/solver/AI 비용을 나누고 인원·모델을 몰래 낮추지 않는다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.02-P**

Given: native A→B→대본 실제서비스  
When: E2E/장애 주입  
Then: 동일 run/version 근거  
Result: NOT_RUN

**TEST-CS-PROOF.02-N**

Given: 문서검사만 성공  
When: E2E/장애 주입  
Then: PRODUCT_NOT_TESTED  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/PROOF/Proof02Tests.cs` · NOT_RUN
- PLAY_MODE: `Assets/ChooGuard/Tests/PlayMode/CSPROOF02PlayTests.cs` · NOT_RUN
- PLAYER_ACCEPTANCE: `qualification/player/CS-PROOF.02.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-001, REQ-070, REQ-074.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-PROOF.03 · A/B/C 사용자효용과 반복 비용

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** StudyResult {conditions,participants,quality,time,censoring,assistance,limits}; 제품정확도를 선호도로 증명하지 않음.

### 필수 상세 명세
- [10-observability-and-study](specs/10-observability-and-study.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `research/usability/protocol.json`
- `research/usability/analysis.py`
- `research/usability/report-template.md`

### 선행 산출물과 소비 단계
- `CS-PROOF.01:candidate` → `CS-PROOF.03:integration` / 조건 `ALWAYS`.
- `CS-PROOF.02:candidate` → `CS-PROOF.03:integration` / 조건 `ALWAYS`.
- `CS-PLAY.07:candidate` → `CS-PROOF.03:integration` / 조건 `ALWAYS`.

### 구현 절차
1. A 고객기존방식, B 동일코어 표/타임라인, C UnityRTS 조건을 고정한다. B는 연구 조건이며 제품모드 추가가 아니다.
2. 순서·난이도·도움·기존AI 허용조건·검수가림을 기록하고 품질이 유지된 총인시를 비교한다.
3. 미완료·중단·보조·초기비용·유지비를 포함하며 20%는 사전합의한 탐색목표로만 쓴다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.03-P**

Given: A/B/C 모든 참가자 결과  
When: 효용분석  
Then: 중단·미완료·도움 분모유지  
Result: NOT_RUN

**TEST-CS-PROOF.03-N**

Given: 좋은 완료자만 분석  
When: 효용분석  
Then: PROTOCOL_VIOLATION  
Result: NOT_RUN

### 실제 시험 매체
- DOCUMENT_REVIEW: `research/proof/03-evaluation-protocol.md` · NOT_RUN

### 연결과 인계
제품 요구: REQ-069, REQ-080, REQ-082, REQ-089.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-05, DISC-07, DISC-08, DISC-09, DISC-11, FREE-001. [출처 등록부](reference/sources.json).

---

## CS-PROOF.04 · 지정 현장의 독립 검증·기관 수용

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PROOF.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** UsePassport {site,scope,manuals,models,qoi,observations,uncertainty,reviews}; 실제 설비 제어 기능 없음.

### 필수 상세 명세
- [10-observability-and-study](specs/10-observability-and-study.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `qualification/field/protocol.json`
- `qualification/field/passport.schema.json`
- `qualification/field/review-ledger.json`

### 선행 산출물과 소비 단계
- `CS-PACK.04:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.
- `CS-PROOF.02:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.
- `CS-SIM.05:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.
- `CS-WORLD.02:candidate` → `CS-PROOF.04:integration` / 조건 `ALWAYS`.

### 구현 절차
1. QoI와 허용오차·독립 split을 사전 고정하고 대상별 규칙·형상·시간·행동·결합을 대조한다.
2. 실행가능·모델검증·현실 관측 연동·기관 지정훈련 수용을 분리한다. surrogate/reference 일치를 현장 검증으로 대체하지 않는다.
3. 안전한 도상/시범 검토와 별도 검수자를 기록한다. 추가 현상은 필요한 독립 증거가 준비될 때만 수용한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-PROOF.04-P**

Given: 독립 holdout·scope·reviewer  
When: 지정 수용  
Then: 해당 QoI/장소/판본만 수용  
Result: NOT_RUN

**TEST-CS-PROOF.04-N**

Given: 같은 개발자료만 재사용  
When: 지정 수용  
Then: NOT_INDEPENDENT  
Result: NOT_RUN

### 실제 시험 매체
- DOCUMENT_REVIEW: `research/proof/04-evaluation-protocol.md` · NOT_RUN

### 연결과 인계
제품 요구: REQ-040, REQ-067, REQ-068, REQ-076, REQ-090.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-11, STD-DTC, TECH-VV. [출처 등록부](reference/sources.json).


# CS-SHIP · 로컬 배포·복구·현장 확장

## CS-SHIP.01 · 오프라인 제품 묶음·업데이트

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SHIP.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ProductManifest {buildLock,contentRefs,modelRefs,capabilities,qualification}; 구매승인은 개발선행이 아님.

### 필수 상세 명세
- [11-release-and-backup](specs/11-release-and-backup.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `packaging/product-manifest.json`
- `scripts/release/Build-Installer.ps1`
- `packaging/update-policy.json`

### 선행 산출물과 소비 단계
- `CS-BOOT.03:candidate` → `CS-SHIP.01:integration` / 조건 `ALWAYS`.
- `CS-PROOF.02:candidate` → `CS-SHIP.01:integration` / 조건 `ALWAYS`.
- `CS-PROOF.04:candidate` → `CS-SHIP.01:qualification` / 조건 `CLAIM_FIELD_USE`.

### 구현 절차
1. 새 source와 dependency lock·모델/콘텐츠 hash를 결속하고 개인정보·키·제한원본을 제외한다.
2. 원격AI·외부 worker는 승인된 환경에서만 사용하고 오프라인은 가용계산·저장·수동편집을 유지한다.
3. 기술 배포와 현장용 qualification을 구별하여 미검증 표식이 설치과정에서 사라지지 않게 한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SHIP.01-P**

Given: 새 PC offline/locked package  
When: 설치·재실행  
Then: local 저장·편집; 미지원 표시  
Result: NOT_RUN

**TEST-CS-SHIP.01-N**

Given: bundle hash 불일치  
When: 설치·재실행  
Then: CONTENT_INTEGRITY_FAIL  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SHIP/Ship01Tests.cs` · NOT_RUN
- PLAYER_DISK: `qualification/player/CS-SHIP.01-disk.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-073, REQ-087.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](reference/sources.json).

---

## CS-SHIP.02 · 백업·스키마 갱신·장애 복구

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SHIP.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** BackupManifest {cut,dbHash,blobHashes,schema,format,scope}; 삭제는 별도 명시된 운영정책에 따른다.

### 필수 상세 명세
- [11-release-and-backup](specs/11-release-and-backup.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `Assets/ChooGuard/Persistence/BackupService.cs`
- `Assets/ChooGuard/Persistence/SchemaUpgrade.cs`
- `packaging/restore-policy.json`

### 선행 산출물과 소비 단계
- `CS-SHIP.01:candidate` → `CS-SHIP.02:integration` / 조건 `ALWAYS`.
- `CS-LAB.01:candidate` → `CS-SHIP.02:integration` / 조건 `ALWAYS`.

### 구현 절차
1. DB와 blob/checkpoint 참조를 일관된 cut으로 백업하고 누락·손상을 검사한다.
2. 이후 새 제품의 schema version 간 업그레이드는 별도 백업·검증·rollback 경로로 수행한다. 이는 초기화 이전 제품 호환을 뜻하지 않는다.
3. 복구후 receipt·예약·outbox·lineage를 검사한다. 해시는 동일성 검사용이며 기관 승인을 증명하는 서명이 아니다.
4. 실행 중 .db 파일만 복사하지 않는다. 백업 API snapshot에서 참조 집합을 구하고 blob GC를 pin한 후 해시 검증→새 폴더→manifest-last로 게시한다.
5. 복구는 현재 DB를 덮지 않고 별도 staging에서 integrity_check/foreign_key_check·receipt·blob 검증 후 전환한다. migration 실패는 원본을 보존한다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SHIP.02-P**

Given: live DB와 checkpoint blobs  
When: API 백업·복원  
Then: consistent cut; 원본보존  
Result: NOT_RUN

**TEST-CS-SHIP.02-N**

Given: blob 누락/WAL 분리  
When: API 백업·복원  
Then: RESTORE_REJECT  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SHIP/Ship02Tests.cs` · NOT_RUN
- PLAYER_DISK: `qualification/player/CS-SHIP.02-disk.json` · NOT_RUN

### 연결과 인계
제품 요구: REQ-001.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: AUD-SQLITE-BACKUP, AUD-SQLITE-WAL. [출처 등록부](reference/sources.json).

---

## CS-SHIP.03 · 새 현장·기관·사건 확장과 운영 인계

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SHIP.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SiteRegistry+ReleaseScope; 물리·기관 수용은 그 범위의 별도 근거로 결속한다.

### 필수 상세 명세
- [11-release-and-backup](specs/11-release-and-backup.md)
- [12-evidence-and-performance](specs/12-evidence-and-performance.md)

### 새 구현 경로
- `content/releases/site-registry.json`
- `packaging/operations-handbook.md`
- `research/adoption/maintenance-ledger.json`

### 선행 산출물과 소비 단계
- `CS-SHIP.02:candidate` → `CS-SHIP.03:integration` / 조건 `ALWAYS`.
- `CS-PACK.01:candidate` → `CS-SHIP.03:integration` / 조건 `ALWAYS`.
- `CS-MODES.03:candidate` → `CS-SHIP.03:integration` / 조건 `ALWAYS`.
- `CS-PROOF.04:candidate` → `CS-SHIP.03:qualification` / 조건 `CLAIM_FIELD_USE`.

### 구현 절차
1. 새 지역/구역/기관 ID를 등록한다. 특정 과거 구역 수나 ID 대응을 강제하지 않는다.
2. 새 현장의 geometry·rules·model scope를 별도로 검토하고 기존 정확도를 자동 복사하지 않는다.
3. 설정/콘텐츠 변경과 코어코드 변경 비용, 유지보수 책임·관심/파일럿/구매를 구분해 남긴다.

### 정확한 인수 oracle · 아직 미실행

**TEST-CS-SHIP.03-P**

Given: 새 site/rule scope  
When: 확장등록  
Then: 새 qualification 필요  
Result: NOT_RUN

**TEST-CS-SHIP.03-N**

Given: 옛 현장 승인 복사  
When: 확장등록  
Then: SCOPE_MISMATCH  
Result: NOT_RUN

### 실제 시험 매체
- EDIT_MODE: `Assets/ChooGuard/Tests/EditMode/SHIP/Ship03Tests.cs` · NOT_RUN

### 연결과 인계
제품 요구: REQ-075, REQ-088, REQ-091.
신규 commit·정확 입력/출력 hash·실행환경·명령·원시 결과·미실행 사유·후행 산출물을 반환한다. 위 시험 파일 경로는 구현할 대상이지 이 문서 ZIP에서 실행한 제품 시험이 아니다.

원문 ID: DISC-02, DISC-04, DISC-07, DISC-08, DISC-09, DISC-10. [출처 등록부](reference/sources.json).
