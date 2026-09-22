<!-- converted from CHOOGuard_Architecture_v1.docx -->

CHOOGuard
전체 아키텍처 설계서
Unity Native · 근거 기반 철도 운영 실험실
작성자가 운영하고, 근거로 비교하며, 훈련 대본으로 내보낸다.

이 문서는 기술 선택·인터페이스·저장·계산·복구·검증의 설계 계약이다. Unity 실행, 물리모델 정확도, 기관 승인을 완료했다는 보고서가 아니다. 실제로 수행한 검사는 패키지의 review/VERIFICATION.md에 별도 기록한다.
전체 설계도 10장, 인터페이스·스키마, 의사결정 16건, 기존 요구 추적표를 함께 제공한다. 그림의 모듈은 구현 목표이며 해당 코드가 모두 존재한다는 뜻이 아니다.

# 문서 지도
00. 문서 계약과 핵심 결정
01. 제품 경계·이해관계자·품질 시나리오
02. C4 전체 구조와 모듈 책임
03. Unity 네이티브 UI·입력·스레드 설계
04. 현실 공간·무료 자산·디지털 트윈 제작 파이프라인
05. 운영 커널과 기관·업무·자원 모델
06. 매뉴얼 컴파일·두 모드·상황 생성
07. 명령·이벤트·지속성의 정확한 의미
08. 다중 물리·운영 결합과 계산 시간
09. 완전 checkpoint·A/B 분기·재현성
10. 데이터·온톨로지·로컬 저장 설계
11. AI 저작·근거 검토·대본 출력
12. 안전·보안·자격·검증 증거
13. 프로세스 통신·배포·운영성
14. 성능·테스트·독립 검증 전략
15. 현재 코드와 EP00~EP11 마이그레이션
16. 아키텍처 비평과 남은 실행 결론
## 다른 읽기 경로
개발 AI: START_HERE → 담당 EP packet → 필요한 장 → 인터페이스와 schema.
검수자: §14–16 → 원시 시험 결과 → 실제 제품 시험은 별도 실행.
전체 그림: CHOOGuard_Architecture_Blueprints_v1.pdf 또는 diagrams/의 SVG.

# 00. 문서 계약과 핵심 결정
ARCH-CHOOGUARD-001 / v1.0.0 / 2026-09-19. PRD v11과 Unity Native UX v2를 구체화하는 전체 아키텍처다. 제품 요구 91개, EP00~EP11, S01~S12를 유지한다. 아키텍처 선택은 설계 결정이고, 외부 라이브러리 설치·Unity 실행·현장 정확도·기관 승인은 아니다.
결정: Unity 네이티브 클라이언트 안에 순수 C# 기반의 모듈형 운영 코어를 두고, 무거운 계산·변환·AI는 장애를 격리할 수 있는 외부 워커로 연결한다. 첫 배포는 단일 PC·로컬 저장·한 작성자다. 업무마다 마이크로서비스, Kafka, Kubernetes, 필수 클라우드, 웹 UI를 도입하지 않는다. headless 실행은 같은 코어의 다른 host이며 한 run을 두 authority가 동시에 실행하지 않는다.
제품의 순서: 작성자가 운영 → 근거 있는 문제 발견 → B안 분기 → 조건을 고정한 비교 → 사람이 선택 → 대본과 실행형 시나리오 출력. LLM이 사건 결과를 창작하거나 정답 운영안을 선결정하지 않는다. 두 사용자 모드는 TUTORIAL과 RANDOM_OPERATIONS_LAB뿐이며 계산 프로파일·기관별 보기·분석은 공통 도구다.
## 문서 간 우선순위
최신 사용자 결정 → PRD v11의 제품·검증 요구 → UX v2의 Unity/입력 요구 → 이 아키텍처의 기술 선택 → 과거 조사/목업. 과거 HTML 프로토타입은 현재 구현 정본이 아니다. v10/v8의 내부 AAA 문구를 상속하지 않는다. 전체 원문과 계약은 basis/에 보존한다.
## 선정 상태

표준·패턴은 오래 검증된 것도 사용한다. C4, DDD, CQRS, Event Sourcing를 2026년 발명이라고 부르지 않는다. 최신 연구는 현장 benchmark에서 검증해 선택한다. [SRC-C4][SRC-EVENT]

# 01. 제품 경계·이해관계자·품질 시나리오
주 사용자는 동일 현장 묶음을 반복 사용하는 훈련 시나리오 작성팀이다. 기관 협조자, 매뉴얼 검수자, 모델 검수자, 훈련 책임자, 배포 담당자를 별도 actor로 모델링한다. 프로그램 사용자 권한과 시뮬레이션 속 기관의 권한은 다르다.
초기 현장은 외부 도착 경계·현장 외부 업무공간·맞이방·승강장·객실을 연결한다. 13개 root RegionId는 확장 기준으로 보존하되 실제 연결·임시 치수가 틀리면 버전 마이그레이션한다. 현실 정보·출처가 없는 장면은 기능 fixture로 사용할 수 있지만 실측 사실로 취급하지 않는다.
## 우선 품질 시나리오

성공은 검토 가능한 대본까지의 총인시와 중대한 공조 모순으로 측정한다. A/B/C 효용 비교에서 3D가 없는 B는 연구 인터페이스이며 새 제품 모드가 아니다. 구현·효용·정량 정확도·기관 활용·사업 도입의 판정을 분리한다. [SRC-PRD]

# 02. C4 전체 구조와 모듈 책임
설계도 A01은 시스템 경계, A02는 배포 프로세스, A03은 컴파일 의존성이다. 양방향 runtime 메시지와 정적인 dependency를 혼동하지 않는다. C4 container는 논리적인 실행 단위이며 반드시 Docker 컨테이너를 뜻하지 않는다. [SRC-C4]

DomainContracts와 ApplicationPorts는 기술을 참조하지 않는 안쪽 경계다. 외부 구현은 port를 구현하고, CompositionRoot만 실제 구현을 결속한다. Domain→SQLite, Domain→Unity, Domain→Python 의존은 허용하지 않는다. DI 프레임워크를 필수로 늘리지 않고 명시적 생성자 조립으로 시작한다.
기본 배포: Unity process(main UI thread + serialized simulation owner) / 필요 시 solver·AI·export child processes / 로컬 SQLite 및 content-addressed files. 실험 headless host는 동일 도메인 라이브러리를 사용해 GUI 없이 배치를 수행하되 별도의 runId를 갖는다. LAN 협업은 추후 기존 authoritative/network 계층에 adapter로 연결한다.

# 03. Unity 네이티브 UI·입력·스레드 설계
UI는 uGUI(Canvas)+TextMeshPro+승인된 Input System 구성을 목표로 한다. 웹, WebView, Electron, 브라우저 DOM을 쓰지 않는다. 현재 저장소의 manifest에서는 com.unity.ugui와 com.unity.inputsystem 직접 선언을 찾지 못했으므로 패키지·backend 호환성 spike를 먼저 수행한다. modules.ui가 있다는 것만으로 전체 uGUI/TMP 설치를 가정하지 않는다. [SRC-MANIFEST][SRC-UUI]
## 런타임 계층
ApplicationRoot에 SimulationSessionBridge, UIStateStore, InputContextRouter, EventSystem, WorldCameraRig, WorldAnchorRegistry, NativeUIRoot를 둔다. HUD/marker/tool-overlay/modal/help canvas는 갱신 빈도·입력 책임에 따라 분리하고, 모든 행마다 Canvas를 만들지 않는다. WorldCamera는 실제 맵을 렌더링하며 HUD는 overlay다. 미니맵과 비교 썸네일만 RenderTexture를 사용한다. [SRC-UX]
입력 소유권: 모달 → IME/TMP 편집 → HUD/스크롤/드래그 → 월드 선택·요청 → 카메라. pointer-down에서 얻은 소유권을 up/cancel까지 유지한다. focus가 텍스트에 있으면 Space·WASD·숫자·Enter는 게임 명령이 아니다. UI 클릭-through와 휠 이중 적용을 실패로 판정한다. [SRC-UINPUT]
UI Presenters는 IOperationsReadPort/ICommandPort/IBranchPort/IAuthoringPort를 사용한다. EventSystem callback은 CommandIntent를 제출할 뿐 운영 데이터 객체를 직접 참조·수정하지 않는다. 새로운 view snapshot은 main thread dispatcher로만 적용한다. UnityEngine.Object, Transform, Physics.Raycast, TMP는 main thread 밖에서 호출하지 않는다.
운영 코어는 한 소유 스레드에서만 변경한다. 첫 연결 단계에서 기존 main-thread 소유를 유지할 수도 있지만, 생산 배포의 목표는 UI와 분리된 dedicated simulation owner다. 기존 코드가 Unity callback을 요구한다면 immutable request/result adapter로 격리한 뒤 이동한다. legacy core를 main thread와 worker thread에서 동시에 호출하지 않는다.
## UI 판정 모델
CommandReceipt의 ACCEPTED는 요청의 접수·원자 기록이지 작업 완료가 아니다. TASK_COMPLETED는 작업별 도메인 이벤트다. 미리보기에서 조회한 entity/authority/rule revision은 확정 시 재검사한다. 전체 simulation tick이 변했다는 이유만으로 모든 명령을 낡았다고 거부하지 않고 해당 명령의 읽기 집합과 권한/규칙 revision을 비교한다.
조회는 AUTHOR_ANALYSIS, AGENCY_OBSERVED scope로 나눈다. scope는 서버/코어에서 검사하고 반환 전에 필터한다. 분석 화면을 열어도 agency KnowledgeLedger를 변경하지 않는다. 모의 상대 기관은 수신한 사실·확인·권한에 따른 policy로 행동하며 LLM 자유 대화로 실행을 결정하지 않는다.
S01~S12는 UX v2의 native surface mapping 그대로 유지한다. 목록 virtualization/pooling, 선택된 marker 우선순위, text escape, 한국어 fallback/IME, 100~200% 글자 확대, keyboard-only 배정, modal focus 복원을 실제 Player로 검증한다. 화면 숨김·LOD·scene unload가 계산·자원 상태를 지우지 않는다.

# 04. 현실 공간·무료 자산·디지털 트윈 제작 파이프라인
맵은 단일 FBX가 아니라 SiteBundle의 공간 표현이다. 동일 PhysicalAssetId에 render mesh, movement/collision, calculation geometry, semantic bindings를 연결한다. 실제 기관·시설과의 연결이 없는 합성 객체는 SyntheticAssetId로 표시한다. 전체 real-world validity는 모델 품질 하나로 판정하지 않는다.
## 입고와 빌드
원본 발견 → 무료판/취득 경로 확인 → raw bytes/hash → 격리 검사 → 축·단위·피벗·파트·rig·clip → import preset → prefab binding → 성능·의미 검수 → signed/검토된 bundle. 출처 상태는 DISCOVERED/ACQUIRED/INSPECTED/IMPORTED/QUALIFIED로 나누고 이전 조사와 새 실행을 구별한다.
무료 우선 기본 후보는 RGS 긴급차량, Quaternius 인물, Kenney 배경·UI, Poly Haven 한국형 소화기, 기존 AED·들것·무전기다. 기존 free-assets.json을 보존한다. 구매비 0원은 가공·한국화·모델 보정 비용 0원이 아니다. 외형으로 인원·정원·물성을 생성하지 않는다. 핵심 객실·통로·문 치수는 근거로 재구성한다.
실제 사진·영상·기준치가 있는 구간에서 MapAnything+COLMAP을 초기 복원·정합 기준으로 시험한다. 원본 카메라 파라미터, CAMERA_Z/RAY_RANGE 구별, 영상 resize, world/camera 변환을 저장한다. fit에 쓰지 않은 check point로 평가한다. 높은 neural confidence를 낮은 계측 불확도로 바꾸지 않는다. [SRC-MAP]
## 좌표와 이동 권위
측량·광역 자료는 CRS와 원점을 보존하고 double 현장 ENU를 canonical frame으로 한다. Unity 좌표는 E,U,N 매핑을 명시하고 handedness 변환에 따른 mesh winding·normal·quaternion을 golden fixture로 검증한다. 단위 m, s, kg, K의 계약을 고정한다. 원천이 기존 X/Y/Z frame이면 mapping version 없이 바꾸지 않는다.
차량 local frame과 station frame을 분리한다. 차 안 사람·장비·사건은 차량 frame에서 상태를 유지하고 정차·도킹 시 transfer portal이 열린다. 사람이 도로 차량/보행/객실 solver 경계를 건널 때 owner epoch를 바꾸는 handoff를 commit하며, 동일 사람을 두 solver가 동시에 소유하지 않는다.
층별 보행 surface와 stair/elevator service를 구분한다. JuPedSim의 2D 영역만으로 수직 이동 전체를 구현했다고 하지 않는다. 승강기에는 호출·대기·정원·이동·인계 상태를 둔다. 경로 availability와 실제 통과량은 서로 다른 값이다.
REALITY_BASELINE 갱신은 새로운 bundle revision을 만든다. 실행 중인 run은 고정된 revision을 유지한다. 변경 정보를 실행에 넣고 싶으면 explicit branch/migration과 재평가를 요청한다. 가상으로 문을 열었다고 현실 관측을 만들지 않는다.

# 05. 운영 커널과 기관·업무·자원 모델
운영 코어는 다기관 업무의 인과·시간·자원 제약을 실행하는 순수 C# 모듈이다. 기존 AuthoritativeShift의 단일 소유권·idempotency·durable commit 원칙을 연결하되, 기존 개인 근접행동 모델을 그대로 확대하지 않는다. [SRC-WORLD][SRC-AUTH]
## 도메인 aggregate
Session은 고정 input binding과 committed boundary를 갖는다. Task는 lifecycle과 복수 Reason를 갖는다. Agency는 CommandScope/KnowledgeLedger를, ResourcePool은 사람·차량·장비의 개별 ID와 capability를, PlanRevision은 조건부 업무와 변경 이유를 갖는다. 사용자 requesterId와 가상 actingAgencyId/actingTeamId를 분리한다.
업무 lifecycle: REQUESTED → ACKNOWLEDGED → RESERVED → MOVING/PREPARING → EXECUTING → COMPLETED → HANDED_OFF → RELEASED. 업무 종류에 따라 필요한 단계만 사용한다. 취소·실패·suspension은 별도 전이와 자원 보상 규칙을 갖는다. 권한·지식·이동·안전·근거 상태를 lifecycle enum에 모두 합치지 않는다.
출입/보고 등 command 종류는 allowlist로 제한한다. rules DSL은 표현식 AST만 허용하며 C#/Python/shell 문자열 실행, 동적 reflection, 원문 안의 도구 지시를 허용하지 않는다.
## 자원 예약
한 Task의 필요한 인력·차량·장비 전체를 하나의 transaction에서 원자 예약한다. 서로 독립적인 여러 task를 다중 선택해 요청할 때는 task별 receipt와 partial policy를 명시한다. 하나의 task 내부에서 '소방차는 확보, 승무원은 실패' 상태를 성공으로 처리하지 않는다. requester가 소유하지 않은 예약을 rollback하지 않는다.
인원 및 장비의 점유 불변식은 sum(a[j,r]*x[j,t]) <= capacity[r,t]다. 개별 resourceId의 exclusive occupancy와 팀/승무원 중복도 검사한다. 구성원을 분리·교대하면 team capability를 재계산한다. 작업 완료와 재투입 가능 시점은 분리한다. [SRC-PRD]
## 지연·공조·정보
메시지는 CREATED/SENT/DELIVERED/ACKNOWLEDGED/APPLIED/EXPIRED로 구분한다. 문서를 화면에 표시한 사실과 모의 기관이 받은 사실, 사람이 이해했다는 annotation을 분리한다. 지연 법칙은 검토된 distribution/model version을 가지며 표본값은 결과 journal에 기록한다.
대기 원인은 constraint evaluation 결과다. Reason에는 code, violatedPredicate, observedValueRef, owner, dependencies, suggestedInputs, sourceClauseRefs, reviewRequired를 둔다. WAIT_FOR_REPORT와 CYCLIC_WAIT를 구별하고, 순환 component가 발견돼도 외부 유입으로 해소되는지 분석해 '교착 확정'과 '가능한 순환대기'를 나눈다.
TAPAAL은 runtime을 대체하는 새 엔진이 아니라 RuleIR/업무망을 제한된 PNML로 투영해 검사하는 도구다. bound·timeout·미지원 전이는 UNKNOWN으로 보고하고, runtime과 golden trace를 대조해 번역 의미를 확인한다. [SRC-TAPAAL]

# 06. 매뉴얼 컴파일·두 모드·상황 생성
## 매뉴얼 파이프라인
SourceDocument(version/hash/access) → ClauseRef(page/section/text span) → 후보 RuleIR → 타입·참조·충돌 검사 → 담당자 적용성 검토 → immutable RuleBundle. 파싱·LLM 추출·검토 승인을 서로 다른 활동으로 저장한다. PDF 이미지 판독이나 미취득 문서는 확인 수준을 드러낸다.
RuleIR 필드: id, revision, applicability(기관·관할·사건·장소), deonticType, trigger, guard AST, requiredCapabilities, effects, handoff, exceptions, provenance, approvalRefs. 규칙의 근거를 짧은 문서 이름 하나로 축약하지 않는다. 관할·판본이 충돌하면 자동 최신 날짜 우선으로 혼합하지 않고 검수로 해소한다.
공학 불변식(자원 보존/중복권위 금지)은 매뉴얼상 재량으로 해제하지 않는다. 매뉴얼이 허용하는 재량은 사유·권한·적용범위를 기록한다. 공개 SOP를 한국 모든 기관의 지휘권 근거로 확대하지 않는다.
## 튜토리얼
튜토리얼은 같은 ScenarioSpec·RuleBundle·ModelLock 위에 LearningOverlay만 추가한다. 안내/힌트 축소/복기의 progression은 운영 값이나 인력 능력을 수정하지 않는다. 정확한 원본 대본이 없는 재구성 콘텐츠는 재구성임을 표시한다. 이벤트 부분순서와 학습목표를 검사하며 하나의 클릭열을 정답으로 강제하지 않는다.
## 일반 랜덤 운영실험
generator는 incident grammar+constraint solver+conditional distributions를 사용한다. 단일 LLM 프롬프트로 미래 사건 전체를 자유 생성하지 않는다. 숨은 사건 정본은 coordinator 측에 두며 author 분석 범위·기관 관측 범위로 projections를 나눈다. 공격·무기 설계 대신 대응 시험용 사건 상태만 다룬다.
FEASIBLE_WITHIN_MODEL/ESCALATION_REQUIRED/CONFLICTED_INPUT/UNSUPPORTED_DOMAIN의 생성 결과를 구별한다. rejection 이유·시도 횟수·원래 분포를 보존해 쉬운 사례만 남기는 편향을 검사한다. 초기 재시도 상한은 설정값 20이며 초과 시 조건 수정 요청이다(운영 규정 아님).
ScenarioSpec은 외생 입력과 조건부 발생 법칙, OperationalPlan은 작성자의 정책/업무 선택이다. 조치로 달라지는 교통·혼잡·보고 생성을 고정 외부 사건으로 잘못 분류하지 않는다. scenario seed/혁신 stream은 기관에게 노출하지 않는다.

# 07. 명령·이벤트·지속성의 정확한 의미
A04는 명령의 정상 경로와 실패 경로를 나타낸다. 이벤트 기반 설계는 모든 CRUD를 event sourcing으로 바꾸자는 뜻이 아니다. 실행 ledger와 plan/review 이력은 불변으로 두고 사용자 설정·캐시·검색 색인은 일반 변경 가능 데이터로 둔다. [SRC-EVENT]
## commit 경로
- Intent를 수신해 size/schema/auth/session을 검증한다. key는 (runId,requesterId,intentId)다.
- 같은 key+동일 semantic fingerprint면 저장된 receipt를 반환한다. 같은 key+다른 payload면 ID_CONFLICT다. 재전송의 wall-time은 fingerprint에서 제외하고 실제 행위 필드·scope·expected revisions는 포함한다.
- 한 authority owner가 read-set·역할·manual revision·resource/target revisions를 검사하고 candidate state/events를 만든다.
- ledger, command receipt, resource reservations, outbox를 같은 DB transaction에서 commit한다. acceptedSeq는 durable commit 후에만 외부에 알린다.
- commit 성공 후 메모리 상태와 projection을 게시하고 outbox가 worker로 boundary/job을 전달한다. commit 실패 시 기존 상태를 보존하고 PERSISTENCE_BLOCKED를 표시한다.
수신 사실(command inbox/audit)과 검증된 domain event를 분리한다. 거부된 요청도 감사에는 남길 수 있으나 TaskStarted를 생성하지 않는다. 디스크 용량 부족으로 감사도 기록할 수 없으면 저장 실패를 숨기지 않는다.
## 외부 작업과 exactly-once
프로세스/네트워크 전송은 at-least-once를 가정한다. 외부 solver는 jobId+inputHash+workerEpoch로 중복 작업을 식별하고 결과를 immutable file로 작성한다. coordinator는 unique resultId와 accepted boundary transaction으로 한 번만 효과를 게시한다. 이를 외부 세계 전체의 exactly-once 보장이라고 부르지 않는다.
worker가 결과를 보냈지만 receipt 응답 전에 연결이 끊기면 같은 job/result로 재조회한다. 현재 branch와 다른 결과, 이미 무효화된 입력, 낮은 epoch는 보관 후 격리하며 현재 상태를 덮지 않는다.
취소는 이미 한 행동을 과거에서 삭제하는 명령이 아니다. 새로운 CancellationRequested/Stopped/ResourceReleased 또는 compensation event다. 물리 진행 중 취소의 가능한 경계도 worker capability로 결정한다.

# 08. 다중 물리·운영 결합과 계산 시간
## authority와 solver의 경계
OPS는 작업·정보·예약의 정본이다. 각 movement/hazard/traffic worker는 계약에 지정된 수량과 내부 상태의 계산 소유자다. SIM은 서로의 출력을 같은 시각·공간·revision에 결속한 accepted snapshot을 게시한다. Domain의 문 열림 상태를 worker가 임의 변경하지 못하고, worker는 해당 opening의 물리 효과만 계산한다.
공통 교환 시간은 Int64 simulationTicks와 clockProfile이다. 첫 제안 quantum은 1마이크로초이며 이는 정확도 약속이 아니다. 기존 SimulationTick은 원래 profile의 단위를 유지하고 explicit mapping으로 연결한다. continuous solver의 내부 dt는 별도이고 target barrier의 seconds로 변환한다. 전이 순서는 (time, microstep, phasePriority, stableSequence)로 버전 고정한다. 같은 시각에 무한 전이를 만들면 ZENO_LIMIT로 중단한다.
## 보수적 동기화
CommittedBoundary(t,b)에서 각 worker가 만족하는 다음 exchange/event horizon을 협상한다. 필수 worker가 t+h에 대해 결과와 restart 정보를 반환하면 consistency·단위·경계 revision·유효범위 검사를 거쳐 accepted field set을 commit한다. t+h의 결과가 완료되기 전에는 t+h를 현재 물리시각으로 표시하지 않는다.
worker 중 하나가 실패하면 이미 전진한 worker를 임의 계속 쓰지 않는다. 마지막 일관 checkpoint에서 재시작하거나, rollback 없는 worker는 기록된 입력으로 다시 실행한다. 이는 OS 프로세스 간 원자적 commit을 가정하는 2PC가 아니라 일관된 snapshot 게시와 재실행 복구 프로토콜이다.
중간 시점의 불연속 사건은 정확한 이벤트 경계를 협상한다. 조기 반환 지원 worker는 boundary를 split할 수 있다. 지원하지 않으면 checkpoint부터 더 짧은 step으로 재계산하거나 해당 오차가 검증된 이산화임을 명시한다. 소방·안전 임계값을 임의 숫자로 정해 step을 건너뛰지 않는다.
## 결합 프로파일


# 08. 계산 경계 · 적용 및 검수
FDS에 일반 목적 실시간 step API나 임의 시점 rollback이 이미 있다고 가정하지 않는다. 기본은 고정된 입력/조작 이력의 batch reference다. 플레이 중 생성된 조작 이력을 기준 solver에서 재검증한다. 인터랙티브 정량값은 stateful validated ROM 또는 실제 지원·검증된 adapter에만 맡긴다. 그런 모델이 없으면 비정량 개발 실행 또는 계산 대기/재시뮬레이션이며 '실시간 정밀 FDS'로 표시하지 않는다. [SRC-FDS][SRC-PHYSICS]
JuPedSim·SUMO는 최신 후보라고 자동 승인하지 않는다. 특히 SUMO emergency의 silent teleport 같은 편의 처리를 quantitative profile에서 제거/탐지한다. FMI 3.0.2는 선택적 교환 규격이며 모든 도구가 FMU, state serialization, early return을 지원한다고 가정하지 않는다. capability probe와 conformance test 결과로 경로를 연다. [SRC-JPS][SRC-SUMO][SRC-FMI]
열·연기·보행·교통의 field transfer에는 variableId, SI unit, frameId, time range, spatial support, boundary revision, interpolation rule, uncertainty, input hash가 필요하다. mass/energy/사람 수 보존, 시간 step 축소 수렴, 공간해상도 민감도, event 직후와 장기 roll-out을 별도 검증한다.
사용자 배속은 wall-time 대비 진행 요청이다. 물리 dt를 임의 키우는 명령이 아니며, 실제 달성 배속과 계산 대기를 표시한다. 요청 수신·heartbeat·timeout은 wall clock을 사용하고 업무시간·가상 회복은 simulation clock을 사용한다.

A05 요약 · 자세한 벡터 도면은 별도 설계도 묶음 참조.

# 09. 완전 checkpoint·A/B 분기·재현성
## 체크포인트는 게임 세이브보다 넓다
CompleteCheckpoint = pinned inputs + committed ledger seq + domain state + scheduled events + resource ownership + in-flight messages + agency knowledge + RNG streams + 각 필수 worker state 또는 certified replay recipe + UI와 분리된 time/epoch.
단순 위치·시드·C# JSON만으로 EXACT checkpoint를 선언하지 않는다. canBranch는 EXACT_STATE, REPLAY_FROM_START, REVIEW_ONLY 중 지원 capability를 반환한다. GPU/병렬 solver는 bitwise repeatability가 보장되지 않을 수 있으므로 EXACT_STATE는 상태를 완전 보존한다는 의미와 별개로 replayToleranceProfile을 가진다. 비트 동일/수치 허용오차/의미 동등/통계적 재현 수준을 구분한다.
## 분기 장벽
새 입력 수신을 정지하고 PauseRequested를 처리한다. 마지막 수락 교환 경계에서 pending commit/outbox/job 상태를 확정하고 worker checkpoint를 취득한다. 모든 필수 blob의 checksum·input lock·time가 맞으면 manifest를 commit한다. UI는 이 응답 후에만 '정지됨/정확 분기 가능'이라고 표시한다. 성공한 parent는 계속 변경 가능하더라도 child의 parentSequence cutoff는 고정이다.
branch는 부모 event prefix를 복사·변경하지 않는 copy-on-write lineage다. child는 own runId/epoch를 받고, 부모의 진행 중 메시지/예약/RNG를 필요한 상태로 복원한다. 현재 시점에서 과거 metadata를 덮어쓰지 않는다. plan edit는 새 PlanRevision이며 runtime cancel과 다르다.
## 공정한 비교
A/B input lock: SiteBundle/RuleBundle/ModelLock/ScenarioSpec/initial checkpoint/QoI contract. external randomness는 named substream + stable process/entity/event key로 대응시킨다. 하나의 global RNG를 순서대로 호출하면 계획 변화가 다른 무작위 수까지 바꿀 수 있으므로 그것만으로 paired 비교를 인정하지 않는다.
같은 외생 혁신이라도 B의 조치로 달라진 혼잡·도착·보고 결과는 B에서 다시 계산한다. 이를 A의 실제 이벤트 tape로 덮어쓰지 않는다. 예측 품질이 다른 profile끼리는 공통 검증 지표만 비교한다. constraint 위반을 빠른 종료시간으로 상쇄하지 않고 Pareto 비지배 대안과 trade-off를 제공한다.
고정 30회는 표본수 정답이 아니다. PRD의 30쌍 초기 예산은 목표 정밀도·변동·사전 종료규칙에 따라 확정한다. 탈락·실패·미완료·unsupported를 전체 분모로 보고하고 선정용 조건과 holdout 평가 조건을 분리한다. 순위가 불확도 안에서 뒤집히면 우열 불명이다.
## 영향 분석·캐시
dependency graph와 semantic diff로 dirty closure를 계산한다. 그래프가 불완전하거나 물리 파급 범위를 제한할 수 없으면 넓은 재계산으로 전환한다. unknown edge가 없다고 영향이 없다고 간주하지 않는다. cache key는 내용·모델·코드·전처리·조작 prefix·환경·불확도 설정 hash를 포함한다. 재사용은 blob과 evidence의 자격이 유효할 때만 가능하다.

# 10. 데이터·온톨로지·로컬 저장 설계
## 세 가지 정본
현실 기준은 SiteBundleVersion/ObservationRevision, 가상 설계는 ScenarioSpec/PlanRevision, 실제 실행은 Run/Event/Checkpoint다. ScriptIR는 선택한 운영안을 문서화한 결과이며 원시 실행을 대신하지 않는다. Citation·Approval·Qualification은 대상 revision을 명시한다.
ID는 Unity instanceID나 Transform 이름이 아니다. 안정된 business ID와 version/content hash를 함께 사용한다. 파일 hash는 실제 bytes에 대해서만 생성한다. 빈 실제 입력을 0·오늘 시각·가짜 해시로 채우지 않는다. 구조 예제의 해시는 synthetic bytes로 계산하고 FIXTURE_ONLY라고 표시한다.
## SQLite + content-addressed files
metadata/ledger/receipt/reservation/outbox/lineage/review는 로컬 SQLite에 저장한다. 대형 영상·메쉬·점군·격자장·solver checkpoint는 artifacts/sha256 경로의 immutable blob로 둔다. 높은 빈도의 모든 grid sample을 JSON 이벤트로 한 개씩 저장하지 않고 frame batch 파일과 index를 사용한다.
SQLite는 한 writer를 직렬화하며 reader는 제한된 snapshot query를 사용한다. WAL+FULL 등 durable policy를 성능과 함께 테스트한다. SQLite 공식 문서에 2026-03-13 수정판의 WAL-reset 이슈가 안내돼 있으므로 실제 로드 binary가 수정된 유지 버전인지 검사한다. 버전 문자열뿐 아니라 source/build를 lock한다. WAL은 network share DB에 사용하지 않는다. [SRC-SQLITE][SRC-SQLITE-FIX]
## blob commit
임시 파일을 전용 directory에 작성 → 길이/hash/형식 검증 → flush → 같은 filesystem의 content-addressed 경로에 publish → 그 후 metadata transaction이 참조한다. 두 저장매체에 걸친 원자성을 주장하지 않는다. DB 참조 없는 orphan blob는 복구 시 GC할 수 있다. 참조는 있는데 blob이 없으면 export/재현을 차단한다. manifest가 읽기 가능해지기 전에 모든 필수 blob이 준비돼야 한다.
SQLite WAL 파일을 남기고 DB 본체만 복사하는 backup은 금지한다. SQLite backup API 또는 정상 checkpoint/close 기반 snapshot과 blob inventory를 묶고 clean install에서 restore를 시험한다. approved evidence와 진행 중 run을 삭제하는 GC는 retention policy와 별도 확인이 필요하다. [SRC-SQLITE]
## 온톨로지
World/Site → Agency/Team/Person/Vehicle/Equipment → Task/Message/Reservation → Scenario/Plan/Run/Event/Checkpoint → Claim/Evidence/Review/Script 관계를 정의한다. PROV-O는 실제 entity/activity/agent provenance, JSON-LD는 교환 표현, SHACL은 offline graph shape 검증에 쓴다. RuleIR는 운영 guard의 실행 표현이며 ontology graph를 런타임 rule engine이라고 부르지 않는다. [SRC-PROV][SRC-SHACL]
검색은 clauseId·기관·사건·판본 필터 + 전문검색을 기본으로 한다. embedding/reranker와 제한된 graph expansion은 평가로 이득이 확인될 때만 추가한다. Neo4j·vector DB·전면 GraphRAG를 필수로 늘리지 않는다. 한국어 전문검색은 조사·형태 변화 recall을 실제 query set에서 검사한다.

# 11. AI 저작·근거 검토·대본 출력
## Typed evidence workflow
timeline/metric extractor가 수치·상태·위반을 계산한다. RetrievalPort는 기관·판본·적용범위·ACL로 허용된 Clause/Evidence만 반환한다. AI는 EvidencePack을 받아 구조화된 ClaimDraft/PlanSuggestion/ScriptIRPatch를 만든다. 반환은 schema·참조·numeric exactness·허용 role/action·injection 검사를 통과한 후 사용자에게 diff로 표시한다.
OBSERVED_IN_RUN은 실제 eventRef, AUTHOR_ANNOTATION은 작성시각·진술, HYPOTHESIS는 미검증 가설, MANUAL_SUPPORTED_PROPOSAL은 적법 clauseRef를 요구한다. REVIEWED_INSTRUCTION/기관 승인 값은 AI 출력 schema에서 금지하며 별도의 authenticated review command로만 생성한다.
AI는 CommandGateway의 수행·승인 port에 연결하지 않는다. 운영안 변경 제안은 DraftPatch일 뿐 직접 dispatch가 아니다. 사용자가 수용하면 APP가 새 PlanRevision과 재검사 요청을 만든다. 본문에 '승인됨'이라는 문자열이 있어도 qualification이 바뀌지 않는다. [SRC-OWASP]
## 사람 검토와 재현
model ID/가중치 revision/prompt hash/retrieval index/document revisions/설정/input evidence IDs를 저장한다. LLM 생성은 비결정적일 수 있으므로 결과 bytes와 검토된 patch를 보존하고 replay 때 다시 호출하지 않는다. 생성 응답 자체를 기존 run의 필수 state로 두지 않는다.
네트워크·provider 실패는 운영 커널에 전파하지 않는다. manual editing, cached approved sources, structural validation을 유지한다. 도메인 자료 외부 반출은 기본 off이며 프로젝트 정책과 사용자 확인으로만 허용한다. API key·raw prompt·개인정보는 기본 진단 trace에 남기지 않는다.
## 공통 ScriptIR
선택한 PlanRevision+대표 run+review → ScriptIR → JSON/Markdown/DOCX formatter. 출력에는 사건·조건·담당·보고/안내·자원·진행자 상황부여·평가·적용범위가 들어간다. 예정시각과 관측시각을 다른 필드로 둔다. 모델에서 3분에 우연히 완료했다고 대본의 모든 후속 업무를 3분 고정으로 만들지 않는다.
외부 Word/HWPX 편집은 semantic change, formatting, review note로 분류한다. ScriptStepId를 잃은 문서는 자동 round-trip을 약속하지 않고 수동 reconciliation으로 표시한다. 의미 변경은 관련 approval을 만료시키며 원시 로그는 불변이다. 문서/실행형 데이터는 같은 ScriptIR/Plan source를 공유한다.
TemplateProfile은 실제 고객 양식·버전·지원 formatter·검수 결과를 명시한다. DOCX 생성 성공은 HWPX 지원이 아니다. 지원되지 않은 양식은 선택 불가와 이유를 표시한다.

# 12. 안전·보안·자격·검증 증거
보안 경계는 사용자 파일, 다운로드 자산, 매뉴얼 본문, AI 출력, 로컬 worker, 선택적 외부 서비스다. 원본이 공식 사이트에서 왔다고 embedded script를 실행하지 않는다. 아카이브 압축 해제는 path traversal/symlink/zip bomb/용량·개수 제한을 검사하고 Unity plugin/DLL은 별도 승인한다.
worker는 allowlist executable, 고정 argv, per-job working directory, 환경 변수 제한, CPU/RAM/disk/time budget, stderr log를 갖는다. stdout 프로토콜과 log를 혼합하지 않는다. remote endpoint를 입력 문서나 LLM 제안으로 변경하지 않는다. local process isolation은 완전한 보안 sandbox라고 주장하지 않으며 기관 배포 정책에 맞춰 OS 권한 격리를 검증한다.
## 자격은 다축이다
Artifact 존재/구조, manual applicability, model validity, numerical validation, independent observation validation, exercise authorization를 개별 facet로 둔다. approved-looking 종합 배지 하나로 합치지 않는다. Qualification은 subject content hash, scope, reviewer/authority evidence, procedure, raw evidence, validFrom/revokedAt을 포함한다. local role 체크는 실제 기관의 법적 권한 증명이 아니다.
전시·기능시험 fixture는 실행 가능하지만 실제 정량 순위/현장 승인 output에서 제외한다. unsupported 현상은 hardcoded 피해율로 채우지 않는다. 위협 상황은 훈련 운영의 배경으로 다루며 공격 방법/전술 최적화·무기 제작·실제 설비 원격제어 경로는 두지 않는다.
## privacy와 불변 로그
PersonId는 가상 또는 가명 ID다. 실제 참여자 이름/음성/서명은 최소 수집·별도 접근통제·retention으로 관리한다. append-only의 목적은 계산 재현이며 개인정보 영구 보존 권한이 아니다. 삭제/비식별화 시 관련 artifact의 재현/검증 범위 변경을 기록한다. 해시체인은 변경 탐지 보조이며 조작 불가능성이나 공인 전자서명은 아니다.
실제 현장자료·LLM 키는 배포 원문/로그에서 제거한다. 암호화는 검증된 OS/기관 저장 정책과 키관리로 수행하며 기본 SQLite가 자체 암호화를 제공한다고 쓰지 않는다.

# 13. 프로세스 통신·배포·운영성
## 기본 IPC를 단순하고 명시적으로 고정
동일 process 내부는 C# typed ports+bounded mailbox. 외부 worker는 부모가 만든 stdin/stdout 파이프를 통한 길이 prefix 프레임의 UTF-8 JSON control protocol v1을 기본으로 한다. 작은 control과 큰 array를 분리해 large payload는 별도 immutable binary artifact로 교환한다. 인터넷 포트·HTTP 서버·웹앱이 기본 필요조건이 아니다.
frame = 4-byte unsigned little-endian payload length + UTF-8 JSON. 최대 control frame 1 MiB는 초깃값이며 설정으로 잠근다. stderr는 진단 전용. messageId/correlationId/runId/epoch/inputHash/schemaVersion/kind를 포함한다. nonce는 command line에 노출하지 않고 bootstrap pipe로 전달한다. protocol major mismatch는 실행 거부다. raw JSON long 시간 값은 signed Int64 decimal string으로 표현해 파서 정밀도 손실을 막는다.
timeout은 CANCEL_PENDING이 될 수 있으며 즉시 안전 중단을 가정하지 않는다. worker가 취소 불가능하면 branch를 중지하고 checkpoint부터 복구한다. 재시도 기본 최대 2회는 transient transport/file 오류에만 적용하고 validation 실패나 의미 mismatch에 반복 적용하지 않는다.
remote compute는 요구가 확인된 경우 HTTPS/mTLS gateway로 제한된 job bundle을 전송하는 후속 adapter다. 로컬 DB나 Unity object를 원격에 공유하지 않는다. gRPC/FMI를 사용하더라도 library/platform/capability를 spike로 확인한다. Unity가 .NET 8/10 DLL을 직접 실행할 수 있다고 가정하지 않는다. 외부 modern .NET host와 Unity-compatible library target을 분리한다. [SRC-DOTNET]
## 배포
Windows x64는 첫 production qualification target이라는 설계 제안이며 현장 OS 계약에서 확인한다. 현재 Unity 6000.3.23f1은 유지하고 scripting backend/render pipeline은 기존 설정을 조회·고정한 뒤 결정한다. uGUI/TMP/InputSystem·SQLite native binding·child-process launch·한글 IME·IL2CPP/AOT를 각각 compatibility gate로 둔다. 패키지 자동 업그레이드로 증거를 무효화하지 않는다.
실행 파일, runtime libraries, ModelLock, SiteBundle inventory, RuleBundle, schemas, SBOM/해시, 제한사항, 호환성 matrix를 릴리스로 묶는다. 과거 run은 과거 lock으로 읽고 마이그레이션은 별도 사본에서 수행한다. 운영계약·매뉴얼 변경을 실행 중 hot patch하지 않는다.
## 관측성과 비용
diagnostic trace는 command preview/commit/projection, worker queue/compute/import, AI retrieval/generation, export/review를 correlationId로 연결한다. OpenTelemetry는 선택적 계측 형식이며 GenAI 관례의 개정 상태를 별도 lock한다. 원시 실행 event ledger는 샘플링하지 않고, 운영 진단 trace는 민감정보 제거 후 샘플링할 수 있다. [SRC-OTEL]
simulation time, monotonic wall duration, 현실 observedAt/ingestedAt, 작성·검수 active time을 분리한다. 총인시는 UI idle만으로 인지 노동을 추정하지 않고 관찰/self-report 표시와 함께 수집한다. CPU/GPU·LLM·storage 비용은 별도 축이다. telemetry exporter 장애가 simulation이나 evidence commit을 막지 않는다.

# 14. 성능·테스트·독립 검증 전략
성능 숫자는 측정 결과가 아니라 고정할 초기 공학 예산이다. 1920x1080 desktop 표현 목표 60fps, 로컬 input feedback p95 100ms 이내, 짧은 업무 미리보기 p95 200ms 이내, durable receipt p95 500ms 이내를 첫 workload에서 검증한다. 물리 계산시간은 별도다. 정확도 유지 조건에서 달성하지 못하면 응답 대기/상태를 명시하고 임의 저정밀 결과를 보내지 않는다. 평가 전 hardware·팀/인원/모델·화면·배경작업을 잠근다.
초기 부하 16팀/64대응인력/120일반인/10도로차량/1편성은 PRD의 시험 가설이지 실제 기관 편성이 아니다. 2기관·소수팀 fixture로 먼저 결정적 의미와 장애 복구를 검사한 뒤 증가시킨다. 13구역 전체와 과거 #54 부하 목표를 조용히 삭제하지 않는다.
## 시험 계층

## 반드시 넣을 반증 시험
commit 직전/직후 crash, 같은 key 다른 payload, preview 이후 권한 변경, 중간 worker 결과만 도착, 이전 branch의 늦은 결과, 불완전 checkpoint, 바뀐 매뉴얼 evidence, OOD, 무효 모델간 비교, 인물 solver 중복 owner, malformed archive, prompt injection, 대본 의미 diff 후 과거 승인 유지, projection filtering 누락을 시험한다.
분기 replay는 same semantic events, checkpoint state, worker tolerance profile을 검사한다. headless 결과와 UI 표시 상태는 같은 committed sequence를 비교한다. 연구 비교군 B/C는 core/hash/AI 입력이 동일해야 한다. 문서 자동검사 숫자를 이 시험의 실행횟수로 계산하지 않는다.
AAA는 내부 제품 목표이고 전체 평균점수로 치명적 결함을 상쇄하지 않는다. 구조·실행·사용자 효용·정량·기관 승인·도입 gate를 각각 NOT_RUN/FAILED/PASSED_WITH_SCOPE로 기록한다. 독립 검수자는 실제 별도 검수일 때만 표기한다.

# 15. 현재 코드와 EP00~EP11 마이그레이션
## 이번 확인 범위
2026-09-19 GitHub connector에서 develop commit aae4867c71c96f34f8fe8a252cddd9aa0412394f를 고정하고 Unity version, manifest, Foundation README, Runtime tree, WorldContracts 및 AuthoritativeShift 1~155행을 읽었다. 전체 clone·전체 코드 리뷰·CI 재실행은 수행하지 않았다. 로컬 원격 bytes 다운로드 시도는 DNS 실패였으며, connector 선택 열람을 전체 바이트 감사로 표현하지 않는다. [SRC-CORE][SRC-WORLD][SRC-AUTH]
README의 TrainingSession fixture는 하나의 역할 행동 기반이다. 그러나 Runtime tree에는 authoritative world, crowd/fire/connected thermal, incident/checkpoint 코드도 존재한다. 따라서 '전체 코어가 없다'고 단정하지 않으며, 이 파일들의 이름을 현장 검증 증거로 인정하지도 않는다.
## 중요한 adapter 변화

소스의 2.5m interaction / 25m interest는 현재 구현의 상수다. 이를 RTS 작성자의 지도 선택 범위로 확장하거나 실제 현장 기준으로 쓰지 않는다. author가 업무를 배정할 수 있는 권한과 실제 가상 팀의 이동/현장 수행 조건은 별도 검증한다. [SRC-AUTH]
## 구현 순서
EP00에서 source/policy/성능·호환성 기준을 고정한다. EP01은 IDs·RuleIR·SiteBundle 계약을 제공한다. EP02는 한 공조 workflow·원자 reservation·durable receipt를 구현한다. EP03의 작은 실제/fixture 맵과 EP04의 native HUD가 같은 typed ports로 연결된다. EP05는 완전 checkpoint·branch·비교를 붙이고 EP06은 같은 코어 위에 두 모드를 올린다.
EP07/08은 field별 기존 adapter와 외부 solver를 shadow benchmark 후 단계적으로 승격한다. EP09는 synthetic logs/고객 양식으로 개발하되 승인 주장을 분리한다. EP10은 작동·효용·정량·기관 lane을 독립 평가한다. EP11은 배포·반복 사용·13구역 확장이다.
실제 candidate 산출물 하나가 소비 단계에 전달되면 되는 경우 생산 EP 전체 완료를 기다리지 않는다. 예: 보행 port fixture만으로 UI를 연결할 수 있으며 현장 보행 accuracy 보고를 UI 제작 선행으로 요구하지 않는다. 반대로 정량 배포 lane에는 해당 관측·모델 검증이 필수다.
아키텍처의 proposed path는 승인된 작업권한이 아니다. 현재 work-order/branch/claim과 경로를 확인한 뒤 수정한다. 기존 public Foundation API를 조용히 변경하지 않고 facade+versioned adapter부터 시작한다. 새 authority가 shadow 상태인 동안 legacy state를 쓰지 않으며 cutover 후에는 run별 writer가 정확히 하나다.

# 16. 아키텍처 비평과 남은 실행 결론
다음은 동일 작성자의 설계 비평이다. 독립 전문가 심사나 제품 AAA 인증이 아니다.

실제 사용자 인터뷰, 업무시간 측정, 에셋 새 취득, Unity 컴파일/런타임, 외부 solver 수치 계산, SQL Unity binding, 기관 검토는 이번 설계에서 하지 않았다. static/schema/SQL fixture/diagram 검사는 그 범위만 검증한다. 원격 #222·source·담당자·보드는 변경하지 않는다.
첫 구현 목표는 두 기관의 요청→배정→인계 업무에서 중복 요청과 자원 충돌을 안전하게 처리하고, native UI로 원인을 읽고, 저장된 A안에서 B안을 분기해 차이를 설명·대본화하는 것이다. 정확도 연구는 같은 경계에 연결하고, 필요한 사용사례의 독립 검증을 채운 뒤 해당 주장을 승인한다.

# 전체 설계도 · A01
운영 입력, 단일 책임 코어, 계산 워커, 불변 기록, 근거 기반 대본의 연결

이 그림은 구현 목표의 데이터·요청 흐름이다. 나머지 아홉 뷰와 확대 가능한 SVG는 설계도 묶음에서 확인한다.

# 원문·기술 근거
각 근거의 확인 범위와 설계상 사용처를 함께 기록했다. 라이브러리의 기능 소개는 CHOOGuard에서의 검증 성공을 뜻하지 않는다. 원본 URL은 클릭하거나 패키지 SOURCES.md에서 확인한다.
SRC-PRD  PRD v11
91개 요구·EP00~11의 제품 기준  [PROVIDED_DOCUMENT]
SRC-UX  Unity Native UX v2
uGUI+TMP, S01~12, 입력 우선순위  [PROVIDED_DOCUMENT]
SRC-CORE  Foundation README
기초 코어 설명; 전체 현행 구현 범위는 별도 소스와 대조  [CONNECTOR_READ]
SRC-WORLD  WorldContracts.cs
WorldCommand, ObservedState, ICommitSink  [CONNECTOR_READ]
SRC-AUTH  AuthoritativeShift.cs
1~155행; 단일 소유 스레드, 거리 제약, 물리 게시  [CONNECTOR_READ_PARTIAL]
SRC-MANIFEST  Unity manifest
uGUI/InputSystem/Addressables 직접 선언 미발견; 설치 여부 전역 추론 금지  [CONNECTOR_READ]
SRC-UNITYVER  Unity version
6000.3.23f1; 실제 컴파일 증거 아님  [CONNECTOR_READ]
SRC-UUI  Unity UI comparison
native runtime framework 선택의 근거  [WEB_PRIMARY_READ]
SRC-UINPUT  Unity Input System UI support
UI/game 입력 구분; 문서 버전은 설치 버전 아님  [WEB_PRIMARY_READ]
SRC-DOTNET  Unity .NET profile support
Unity 관리 코드와 외부 .NET 프로세스 구분  [WEB_PRIMARY_READ]
SRC-C4  C4 model diagrams
context/container/component와 동작 뷰; 성숙한 표기법  [WEB_PRIMARY_READ]
SRC-EVENT  Event Sourcing pattern
원시 이벤트와 투영·복원, 적용 비용  [WEB_PRIMARY_READ]
SRC-SQLITE  SQLite WAL
동일 호스트·단일 writer·WAL 파일 보존·내구성  [WEB_PRIMARY_READ]
SRC-SQLITE-FIX  SQLite 3.51.3 release
2026-03-13 WAL-reset 수정; 검증된 수정판 이상 필요  [WEB_PRIMARY_READ]
SRC-TAPAAL  TAPAAL features
timed/colored/stochastic nets, 독립 검증 엔진  [WEB_PRIMARY_READ]
SRC-JPS  JuPedSim models
보행 모델 비교; 층간/운영 모델은 별도  [WEB_PRIMARY_READ]
SRC-SUMO  SUMO emergency
silent teleport 경고; 한국 현장 보정 필요  [WEB_PRIMARY_READ]
SRC-FDS  NIST FDS
화재 열·연기 기준 계산, Evac 중단 범위  [WEB_PRIMARY_READ]
SRC-PHYSICS  NVIDIA PhysicsNeMo
신경 대체모델 훈련 프레임워크; 현장 모델 아님  [WEB_PRIMARY_READ]
SRC-MAP  MapAnything
학습 기반 metric 복원; 독립 정합 별도  [WEB_PRIMARY_READ]
SRC-FMI  FMI 3.0.2
2024-11-27 표준; state restore 등 capability별  [WEB_PRIMARY_READ]
SRC-PROV  W3C PROV-O
출처·실행·생성 결과 관계  [WEB_PRIMARY_READ]
SRC-SHACL  W3C SHACL
RDF 그래프 제약 검사; ontology 자체가 런타임 아님  [WEB_PRIMARY_READ]
SRC-OTEL  OpenTelemetry specification
진단용 trace/metrics. 제품 증거 ledger와 분리  [WEB_PRIMARY_READ]
SRC-OWASP  OWASP LLM01:2025
문서·도구출력 간접지시와 권한 격리  [WEB_PRIMARY_READ]
| 기준 | 결정 |
| --- | --- |
| 제품 계약 | PRD v11 · 91개 요구 · EP00–EP11 |
| 사용자 경험 | Unity Native UX v2 · uGUI/TMP · S01–S12 |
| 구조 | 로컬 모듈형 운영 코어 + 격리 계산 워커 |
| 문서 상태 | 구현 인계용 설계 · 실제 제품/현장 수용 전 |
| 기준일 | 2026-09-19 · 아키텍처 1.0.0 |
| 구분 | 이 설계의 선택 | 아직 증명해야 할 것 |
| --- | --- | --- |
| 확정 방향 | native uGUI/TMP, 단일 논리 소유권, immutable run, 두 모드, 무료 자산 우선 | Unity에서 실제 동작 |
| 초기 기술 | C# 운영 코어, SQLite, process worker, JSON Schema, 명시적 IPC | 대상 OS·backend·성능·복구 |
| 현상별 후보 | 기존 모델, JuPedSim, SUMO, FDS, ROM/PhysicsNeMo | 개별 사용사례 정확도·비용·복원 |
| 선택 연구 | FMI 교환, 학습 기반 복원, 추가 그래프 검색·가속 | 후보 대비 개선과 유지 비용 |
| 금지 주장 | 설계도 = 구현, 최신 도구 = 현장 검증, 파일명 = 확보 증거 | 실제 증거 전에는 승인하지 않음 |
| 상황 | 시스템 응답 | 검증할 성질 |
| --- | --- | --- |
| 같은 요청을 두 번 전달 | 같은 결과를 조회하고 자원을 중복 점유하지 않음 | idempotency, durable receipt |
| 두 업무가 같은 자원 요구 | 한 개의 원자 예약만 성립; 나머지는 이유 제공 | 자원 보존·동시성 |
| 저장 실패 | 승인 응답과 새 상태 게시를 중단 | 승인 전 내구성 |
| 물리 결과가 뒤늦게 도착 | branch/input/epoch/time이 다르면 적용하지 않음 | 오래된 결과 격리 |
| 사용자가 B안 분기 | 완전 checkpoint 또는 적법 replay 경로 검증 | 원본 불변·복원 가능성 |
| AI 또는 인터넷 장애 | 운영·저장·기존 근거·수동 작성 유지 | 장애 격리 |
| 카메라·글자 크기 변경 | 연산 상태·결과가 바뀌지 않음 | 표현/계산 분리 |
| 규칙 판본 변경 | 기존 실행은 보존, 관련 결과 자격은 STALE | 변경 영향·출처 |
| ID | 모듈 | 정본 책임 | 하지 않는 일 |
| --- | --- | --- | --- |
| UI | NativePresentation | 선택·카메라·열린 창·편집 버퍼 | 업무 완료·물리값 변경 |
| APP | ApplicationFacade | 명령·조회 경계, 사용자·보기 권한 | 매뉴얼 자체 판단 창작 |
| OPS | OperationsCore | 업무·권한·예약·메시지·기관 지식 | Unity API·LLM 호출 |
| CONTENT | ContentAndRules | SiteBundle, ScenarioSpec, Plan, RuleIR 판본 | 원문 없는 승인 |
| SIM | SimulationCoordinator | 교환 경계·양 소유권·결과 qualification | solver 수치해법 재발명 |
| EXPERIMENT | ExperimentService | checkpoint·분기·비교·dirty graph | 미래 결과의 자동 정답화 |
| AUTHOR | AuthoringService | 근거 pack·ScriptIR·양식·의미 diff | 실험 로그 수정 |
| EVIDENCE | QualificationRegistry | 결과별 검증·승인·만료·적용범위 | 단일 만능 PASS |
| STORE | LocalPersistence | durable commit·outbox·blob·projection | 원격 공유 폴더 WAL 실행 |
| WORKERS | WorkerAdapters | 수치 계산·문서 변환·복원·체크포인트 | UI/승인 DB 직접 수정 |
| AI | AIProviderAdapter | 승인된 근거로 추출·설명·제안 | 실행 shell·기관 명령 |
| LEGACY | FoundationAdapters | 기존 계약·물리·공간 코드의 보존·번역 | 새 authority와 이중 적용 |
| worker | 초기 역할 | 실제 필요한 capability 검사 |
| --- | --- | --- |
| 기존 C# simulation | baseline/fixture·재사용 후보 | field ownership, complete checkpoint, 결합 오차 |
| JuPedSim | 선택 보행·국소 이동 | step, 상태 재구성, 층간 handoff, replay 동등성 |
| SUMO | 외부 도로/철도 운영 | state save/load·RNG·pending TraCI, teleport 감시 |
| FDS | 오프라인 기준 화재 계산 | 입력·restart 제약, 개구 이력, mesh/time convergence |
| ROM/PhysicsNeMo | 검증된 과도상태 가속 | input/hidden state/OOD/conservation/checkpoint |
| 계층 | 검증 내용 | 이 단계로 주장할 수 없는 것 |
| --- | --- | --- |
| 계약/정적 | schema·참조·의존성·양 owner·원문 연결 | 실제 runtime 성공 |
| 순수 코어 | 명령 원자성·예약·타임라인·난수·전이 | 실제 기관 절차 적합 |
| persistence/IPC | crash point·중복·partial frame·disk full·backup | 현장 정확도 |
| Unity native | input capture·IME·폰트·pooling·모달·Player | 외부 solver 정확도 |
| solver/결합 | baseline·관측·수렴·유효범위·restart·handoff | 실제 대피 안전 승인 |
| 사용자 | A/B/C·총인시·완료/탈락·근거 이해·출력 수정 | 물리 정확도 |
| 기관 | 명시된 사용사례의 절차·훈련 적용 검토 | 보편 안전/최적성 |
| 기존 확인 대상 | 재사용 | 신규 의미 / 검증 |
| --- | --- | --- |
| WorldCommand/CommandReceipt | ID·revision·duplicate·거부 이유 | AuthorIntent requester와 acting agency/team 구분 |
| ICommitSink.Append | flush 전 acceptance 금지 | transaction receipt/reservation/outbox 결속 |
| AuthoritativeShift | 단일 owner·관측 projection·prepared physical 연결 | author plan 편집을 개인의 근접 action과 분리 |
| ObservedState | raw checkpoint·미래 사건 숨김 | 기관 보기와 분석 projection 각각 ACL |
| WorldState/physical checkpoints | frame/tick/definition hash 원칙 | 전체 workflow·message·worker manifest 범위 검사 |
| 기존 crowd/fire/thermal 코드 | baseline·native adapter 후보 | field owner·실제 과제 benchmark·더 나은 모델 비교 |
| 반박 | 보완한 결정 | 남은 실제 증거 |
| --- | --- | --- |
| 최신 도구를 모두 연결해 과설계한다 | 모듈형 코어, 필요한 worker만, 네트워크/메시지브로커 비필수 | 첫 사용사례 비용 |
| Unity 웹앱처럼 또 만들 수 있다 | uGUI/TMP·Game View·input router 강제 | native Player 시험 |
| 현행 코드를 무시하고 새 엔진을 만든다 | pin SHA·WorldContracts/authoritative adapter·cutover | baseline 회귀 |
| FDS가 실시간 rollback 가능하다고 가정한다 | batch reference 기본·capability probe·정직한 계산 대기 | adapter runtime 검증 |
| 저장 전에 UI가 성공을 보인다 | DB commit 후 receipt·outbox | crash injection |
| checkpoint가 시드만 저장한다 | task/message/예약/worker/RNG/barrier manifest | restart 동등성 |
| 최신 데이터로 과거 실험이 변한다 | frozen bundle·explicit branch·qualification invalidation | upgrade 시험 |
| 두 mode가 달라져 학습이 왜곡된다 | 안내와 사건 생성만 달라짐·동일 core lock | same-input equivalence |
| AI가 매뉴얼의 권한을 창작한다 | proposed RuleIR·read-only AI·별도 review | adversarial 평가 |
| 계획 점수가 현장 안전으로 보인다 | 다축 자격·범위·기관 수용 분리 | 실제 independent validation |
| 그림은 있지만 API가 불명확하다 | 10개 schema·예제·SQL·port 및 IPC 계약 | C# compile와 실제 adapter |
| 분산 장애를 exactly-once로 포장한다 | at-least-once·dedup·멱등 결과게시·replay recovery | 다중 crash interleaving |