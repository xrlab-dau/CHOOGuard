# CS-PLAY · Unity 네이티브 RTS 운영 작업공간

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-PLAY.01 · native 입력 소유권·IME

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** InputOwner {context,pointerId,focus,modal}; 소비한 event를 하위 입력에 재전달하지 않는다.

### 필수 상세 명세
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-PLAY.02 · 팀·업무 선택과 명령 미리보기

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SelectionSet→CommandIntent→PreviewResult→CommandReceipt; UI 수락은 업무 완료가 아님.

### 필수 상세 명세
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

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

원문 ID: GAME-EMERGENCY, GAME-RESCUEHQ. [출처 등록부](../reference/sources.json).

---

## CS-PLAY.03 · 게임 HUD·카메라·미니맵

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** UIState {selection,camera,openPanels,textScale}; 운영 상태와 수명을 분리.

### 필수 상세 명세
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-PLAY.04 · 복수 원인·기관별 정보·작업 오버레이

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ReasonView/AgencyTimeline는 read-only; pause는 코어 ack 이후 표시.

### 필수 상세 명세
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-PLAY.05 · 접근성·재배정·텍스트와 반복 조작 효율

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.05 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Preferences는 물리·정보·업무 결과를 수정하지 않는다.

### 필수 상세 명세
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-PLAY.06 · 체크포인트·분기·A/B 비교 네이티브 화면

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.06 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** CheckpointCapabilities→BranchRequest; ComparisonReport→read-only UI; 기록 replay와 새 branch·운영안 수정 구분.

### 필수 상세 명세
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-PLAY.07 · 동일 코어의 연구용 표·타임라인 비교 인터페이스

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PLAY.07 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** StudyConfiguration {condition, coreLock, assistancePolicy, informationScope, caseId}; 연구 전용 condition이며 사용자 모드가 아님.

### 필수 상세 명세
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)
- [10-observability-and-study](../specs/10-observability-and-study.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).
