# CS-BOOT · 새 Unity 제품의 부팅·입력·빌드 기반

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-BOOT.01 · 새 프로젝트·의존성 잠금

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-BOOT.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** BuildBaseline {editorVersion, packageLockSha256, renderPipeline, backend, os, nativePlugins, buildReceiptRef}; 모든 값은 새 실행에서 기록.

### 필수 상세 명세
- [01-build-and-assemblies](../specs/01-build-and-assemblies.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-BOOT.02 · assembly 경계와 신규 공개 계약

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-BOOT.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** IOperationsPort.PreviewAsync(CommandIntent,CancellationToken); SubmitAsync(CommandIntent,CancellationToken); ReadReceiptAsync(ReceiptKey,CancellationToken); ReadProjectionAsync(ProjectionQuery,CancellationToken). 정확 타입은 specs/02-wire-and-ports.md.

### 필수 상세 명세
- [01-build-and-assemblies](../specs/01-build-and-assemblies.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

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

원문 ID: AUD-UNITY-ASSEMBLY. [출처 등록부](../reference/sources.json).

---

## CS-BOOT.03 · 새 테스트·빌드 실행 경로

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-BOOT.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TestReceipt {testSet, commit, environment, startedAt, command, resultFiles, status, notRunReason}; CI credential은 저장소에 넣지 않는다.

### 필수 상세 명세
- [01-build-and-assemblies](../specs/01-build-and-assemblies.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

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

원문 ID: AUD-UNITY-ASSEMBLY. [출처 등록부](../reference/sources.json).
