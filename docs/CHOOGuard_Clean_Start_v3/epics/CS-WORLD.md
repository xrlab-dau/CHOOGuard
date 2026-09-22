# CS-WORLD · 현실 기반 철도 운영 공간

[제품 기준](../PRODUCT_BASELINE.md) · [공통 계약](../CONTRACTS.md) · [검수 보고](../review/REVIEW.md)

## CS-WORLD.01 · 처음부터 만드는 2공간·문 fixture

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.01 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** WorldAnchorMap {entityId,transformRef,frameId,representationKind}; Unity Transform은 표시 수단.

### 필수 상세 명세
- [01-build-and-assemblies](../specs/01-build-and-assemblies.md)
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)

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

원문 ID: 사용자 제품 요구/이 문서의 설계 계약. [출처 등록부](../reference/sources.json).

---

## CS-WORLD.02 · 첫 철도 공간과 무료 시각자산 제작

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.02 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** GeometryReceipt {assetId,sourceHash,transform,units,parts,residuals,scope}; 실제 맵 수용에는 현장 입력이 추가로 필요.

### 필수 상세 명세
- [01-build-and-assemblies](../specs/01-build-and-assemblies.md)
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)

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

원문 ID: FREE-001, FREE-003, FREE-018, MAT-BIPA-BUSAN3, MAT-KTX-I, TECH-MAPANYTHING, TECH-UNITY-FBX. [출처 등록부](../reference/sources.json).

---

## CS-WORLD.03 · 운영 객체·월드마커·층별 보기

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.03 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** SessionProjection: schemas/SessionProjection.schema.json의 entitiesRef/tasksRef/reasonsRef를 해석하며 run/revision/viewScope를 확인한다. 표현 계층은 domain을 직접 쓰지 않는다.

### 필수 상세 명세
- [01-build-and-assemblies](../specs/01-build-and-assemblies.md)
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)

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

원문 ID: TECH-UNITY-LOAD. [출처 등록부](../reference/sources.json).

---

## CS-WORLD.04 · 구역 준비·로딩·차량 프레임 연결

**상태:** SPECIFIED_NOT_IMPLEMENTED / 제품 인수시험 NOT_RUN  
**산출물:** A-CS-WORLD.04 · contract/candidate/integration/qualification의 별도 인계  
**목적/계약:** RegionReadiness {regionId,visual,collision,path,state}; DockLink {vehicleFrame,siteFrame,stopConfirmed,doorState}.

### 필수 상세 명세
- [01-build-and-assemblies](../specs/01-build-and-assemblies.md)
- [06-native-surfaces](../specs/06-native-surfaces.md)
- [07-content-and-rule-contract](../specs/07-content-and-rule-contract.md)

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

원문 ID: TECH-UNITY-LOAD. [출처 등록부](../reference/sources.json).
