# CS-SHIP · 로컬 배포·복구·현장 확장

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-SHIP.01 · 오프라인 제품 묶음·업데이트

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SHIP.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** ProductManifest {buildLock,contentRefs,modelRefs,capabilities,qualification}; 구매승인은 개발선행이 아님.

### 필수 상세 명세
- [11-release-and-backup](../specs/11-release-and-backup.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-SHIP.02 · 백업·스키마 갱신·장애 복구

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SHIP.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** BackupManifest {cut,dbHash,blobHashes,schema,format,scope}; 삭제는 별도 명시된 운영정책에 따른다.

### 필수 상세 명세
- [11-release-and-backup](../specs/11-release-and-backup.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

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

원문 ID: AUD-SQLITE-BACKUP, AUD-SQLITE-WAL. [출처 등록부](../reference/sources.json).

---

## CS-SHIP.03 · 새 현장·기관·사건 확장과 운영 인계

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-SHIP.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SiteRegistry+ReleaseScope; 물리·기관 수용은 그 범위의 별도 근거로 결속한다.

### 필수 상세 명세
- [11-release-and-backup](../specs/11-release-and-backup.md)
- [12-evidence-and-performance](../specs/12-evidence-and-performance.md)

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

원문 ID: DISC-02, DISC-04, DISC-07, DISC-08, DISC-09, DISC-10. [출처 등록부](../reference/sources.json).
