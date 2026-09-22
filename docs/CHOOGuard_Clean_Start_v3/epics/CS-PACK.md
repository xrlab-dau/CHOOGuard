# CS-PACK · 현장·기관 매뉴얼·자료 패키지 제작

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-PACK.01 · 새 공간·기관·시간·데이터 식별자

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SiteBundle {id,revision,frames,regions,portals,entities,sourceRefs,qualification}; ScenarioSpec와 OperationalPlan은 별도 revision.

### 필수 상세 명세
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: STD-JSONLD, STD-PROV. [출처 등록부](../reference/sources.json).

---

## CS-PACK.02 · 기관 매뉴얼을 검수 가능한 규칙으로 구성

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** RuleClause {id,revision,kind,scope,guard,effect,handoff,exception,sourceLocator,review}; DTO는 예외를 검토자료로 유지.

### 필수 상세 명세
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: MAN-KORAIL, MAN-MEDICAL, MAN-SOP. [출처 등록부](../reference/sources.json).

---

## CS-PACK.03 · 무료 원본 입고와 파일 신뢰경계

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** AssetReceipt {id,sourceUrl,tier,rawHash,formats,rigClips,parts,importStatus,limits}; 실제 바이너리가 없으면 rawHash=null.

### 필수 상세 명세
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: FREE-001, FREE-010, FREE-011, FREE-018, MAT-BIPA-BUSAN3, MAT-KTX-I. [출처 등록부](../reference/sources.json).

---

## CS-PACK.04 · 실제 현장 입력·현실 관측 갱신

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-PACK.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** Observation {targetId,observedAt,receivedAt,value,unit,sourceHash,quality}; 현실 자료 없음은 기술 개발의 전역 blocker가 아님.

### 필수 상세 명세
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)
- [02-wire-and-ports](../specs/02-wire-and-ports.md)

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

원문 ID: MAN-KORAIL, STD-DTC, STD-SOSA. [출처 등록부](../reference/sources.json).
