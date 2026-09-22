# CS-SCRIPT · 선택 운영안의 근거 기반 대본 저작

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-SCRIPT.01 · 허용 근거 조회와 규칙/설명 후보

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** AssistantProposal {type,claims,evidenceRefs,missing,patchDraft}; 결정적 상태 계산은 코어 담당.

### 필수 상세 명세
- [09-script-and-approval](../specs/09-script-and-approval.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-SCRIPT.02 · 조건부 운영안·ScriptIR

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ScriptIR {revision,plan,scenario,steps,roles,conditions,evidence,qualifications}; 내용변경은 새 revision.

### 필수 상세 명세
- [09-script-and-approval](../specs/09-script-and-approval.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: REF-MSEL. [출처 등록부](../reference/sources.json).

---

## CS-SCRIPT.03 · native 대본 편집·검토 상태

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ReviewDecision {reviewer,role,scope,revision,evidence,time,status}; focus와 UI가 승인권을 부여하지 않음.

### 필수 상세 명세
- [09-script-and-approval](../specs/09-script-and-approval.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-SCRIPT.04 · 고객 양식 출력·재로드·수정 회수

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SCRIPT.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** TemplateProfile {format,revision,fields,exporter,qualification}; 결과는 임의 script를 실행하지 않는다.

### 필수 상세 명세
- [09-script-and-approval](../specs/09-script-and-approval.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: DISC-05, DISC-07. [출처 등록부](../reference/sources.json).
