# CHOOGuard 통합 Story

<!-- AUTO-GENERATED: plan.json + state/progress.json; edit JSON, then active_plan.py render -->

## CS-EXEC.01.01 — CHOOGuard 통합 제품 구현

상태 **IN_PROGRESS** / 수용 **NOT_ACCEPTED**

대표 게임 11종과 지원 라이브러리 3개를 비교하고 OSS 6개 저장소의 실제 소스를 검토했다. 선택한 건물에서만 지원 요청을 제공하는 UI를 현재 Unity에서 검증했고, 공개 그래프·AI 예측 패널을 제거했다. OpenRA Wait의 실제 코드를 이식해 복귀→재출동 준비→가용 상태를 연결했다. Jev 값 0.55가 내부 식에서 28.5초로 계산되고 실제 승인 시간 29초 뒤 가용 상태로 전환됐으며, 두 팀 모두 443초에 복귀·준비를 완료했다. 시간 계수는 훈련용 가정이고 전체 프로덕션·실측 디지털 트윈 수용은 NOT_ACCEPTED다. 실제 재고·서비스 범위·도시 생활·런타임 건설·저장과 재현·장기 플레이 검수가 남아 있다.

이 도구의 PASS는 관리 구조·참조 정합만 뜻하며 제품 실행·완료·기관 수용 검증이 아니다.

## 기능 체크리스트

아래 영역과 legacy ID는 별도 활성 Story·승인·인계 단위가 아니다. ID는 정본의 원문 객체 전체를 참조한다.

정본: [docs/CHOOGuard_Story_Plan_v4/plan.json](../../docs/CHOOGuard_Story_Plan_v4/plan.json)
전환 digest: `1399115c47999aae7825233666c4c3471fc2bdda43f99000d45f98c1718c65a4`

### 부팅·입력·빌드 · PARTIAL

content-validation 실행에서 BOOT0101 55/55, BOOT0201 44/44, BOOT0202 17/17 및 Bootstrap PlayMode 1/1. 기존 순수 모듈 compiler reference·font whitelist 자동 시험 실패는 해소됐다. 당시 PACK0102 전용 재시험에서 BOOT는 재실행하지 않았다. Windows 빌드·실행 및 수동 제품 수용은 미실시/미완료로 PARTIAL 유지. 후속 operations-mailbox-retest에서 BOOT0101 55/55, BOOT0201 46/46, BOOT0202 17/17 EditMode 통과; emitted pure DLL oracle Passed. 새 Windows NOT_RUN receipt 1개는 Windows 빌드 통과 증거가 아니다. 이후 열린 Editor focused 실행에서 BOOT0201 45/47로 2 FAIL: 실제 test asmdef에 추가된 Persistence가 expected refs에서 누락됐다. expected/정상 음성 fixture/누락 반례 3곳 수정 완료, 수정 후 실행은 NOT_RUN. 기존 FAIL은 보존한다.

원문 ID: `CS-BOOT.01.01`, `CS-BOOT.01.02`, `CS-BOOT.02.01`, `CS-BOOT.02.02`, `CS-BOOT.02.03`, `CS-BOOT.03.01`, `CS-BOOT.03.02`

- 증거: [docs/agent-handoffs/CS-BOOT.01.01/IMPLEMENTATION.json](../../docs/agent-handoffs/CS-BOOT.01.01/IMPLEMENTATION.json)
- 증거: [docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json](../../docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json)

### 자료 패키지 · PARTIAL

BCL+Contracts 기반 strict bounded raw JSON parser와 reference skeleton schemas·semantic relations를 구현·시험했다. content-validation에서 PACK0101 14/14, PreparedAssetImportTests 7/7, PACK0102 79/80(최대 revision fixture 구성 오류 1건). 이후 PACK0102만 수정·확장하여 독립 revision 조합 5개 포함 83/83 통과; 다른 통과 suite는 재실행하지 않았다. 원본 59파일 임포트 증거 유지. 후속 two-agency 실행에서 synthetic 두 기관 fixture 및 trusted test-only projection·관계 검증을 완료했다: PACK0101 14/14, PACK0102 83/83, PACK0103 56/56, 합계 153/153. 완전한 schema backend·geometry·Scenario conditions·OperationalPlan·SourceReceipt·qualification 및 full operational·durable·현장·Windows 수용은 미완료다. fixture 통과는 전체 green run이나 제품 수용이 아니다. 후속 operations-mailbox-retest에서 PACK0101 14/14, PACK0102 83/83, PACK0103 56/56 EditMode 재통과. 후속 RuleTypes/RuleCatalog와 AssetIntakeValidator foundation 부분 구현 및 독립 EditMode 관련 시험 통과를 확인했다. catalog/validator는 완전한 업무 실행·blob resolver·durable 저장 통합이 아니다. 해당 실행 overall FAIL(mode 보존 실패)은 유지한다.

원문 ID: `CS-PACK.01.01`, `CS-PACK.01.02`, `CS-PACK.01.03`, `CS-PACK.02.01`, `CS-PACK.02.02`, `CS-PACK.03.01`, `CS-PACK.03.02`, `CS-PACK.04.01`, `CS-PACK.04.02`

- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/two-agency-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/two-agency-validation-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json)

### 다기관 운영 커널 · PARTIAL

메모리 내 OperationsSession·불변 RunState 구현: process-local run 독점, tick/priority/sequence 정렬, admission 입력·collections 복사, 재진입·동시 쓰기 거부, 실패 전 성공 prefix만 반영하고 실패+후속 명령 보존 및 retry, 분리된 읽기 상태, overflow/dispose 경계. operations-mailbox-retest에서 OPS 21/21 및 관련 경계 시험 통과. 해당 mailbox 실행 당시 IOperationsPort/IRunStore 구현은 범위 밖이었다. 후속 dispatcher preflight(readSet 사전검사), AuthorityPolicy/SupportRequest 및 ReservationPlanner foundation은 부분 구현됐다. 이는 실제 업무권한/readSet/reservation의 종단 통합 완료가 아니다. 후속으로 SqliteRunStore IRunStore·idempotent durable commit과 v2 outbox claim/lease/redelivery/ack가 부분 구현됐다. native host 5개 시험 및 schema 4table 일치 PASS, 변경 한정 검수 구체적 결함 0건. production materializer/decoder/transport/IOperationsPort·SessionProjection/UI 종단 조립 및 simulation result 원자 적용은 미완료다. dispatcher 불일치 ack 직접 회귀는 후속 Astra low가 보강 중이다. callback 외부 부수효과 rollback/exactly-once/at-most-once 보장은 없다. full operational·durable·현장·Windows·전체 제품 수용 미완료로 PARTIAL 유지. 독립 EditMode의 foundation 시험 통과와 overall FAIL(mode 보존 실패)을 구분하며 guard/planner를 전체 업무 완료로 승격하지 않는다.
2026-09-21: 후속 MVP 전용 로컬 snapshot 기반 명령/receipt 저장과 재실행 복구를 구현하고 열린 Editor의7 focused assertion을 확인했다. 기존 K02 production SQLite 조립을 완료한 것은 아니다. 후속 기관 핵심 흐름: 중앙119 실제 좌표→지원요청/도로왕복/현장가용/작업ACK,참조군중50명 대피 후Recovery nativePASS. 실제기관전체절차/자원·설비운영은부분구현.

원문 ID: `CS-OPS.01.01`, `CS-OPS.01.02`, `CS-OPS.01.03`, `CS-OPS.02.01`, `CS-OPS.02.02`, `CS-OPS.02.03`, `CS-OPS.02.04`, `CS-OPS.03.01`, `CS-OPS.03.02`, `CS-OPS.04.01`, `CS-OPS.04.02`, `CS-OPS.05.01`, `CS-OPS.05.02`, `CS-OPS.05.03`, `CS-OPS.06.01`, `CS-OPS.06.02`, `CS-OPS.07.01`, `CS-OPS.07.02`

- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-validation-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-production-benchmark-oss-kernel.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-production-benchmark-oss-kernel.json)

### 철도 운영 공간 · PARTIAL

WorldEntityAnchor/FixtureBuilder foundation 부분 구현. 독립 foundations 실행에서 CSWORLD0101/0102는 dirty Untitled/fixture 제약으로 NOT_RUN이며 431건 통과에 포함되지 않는다. 후속 FixtureBuilder 및 CSWORLD0101/0102 시험 소스의 preview lifecycle 3파일 수정은 존재하나 Unity compile/tests NOT_RUN, saved fixture 생성·SaveScene 성공·reload 동등성 NOT_VERIFIED다. dirty unsaved scene 전제 부재 시 Ignore되는 회귀는 수용 증거가 아니다. Jev World 검수는 preview 수정 전 snapshot으로 수정 후 검증이 아니다. 최신 열린 Editor discovery에서 World0101 14건·0102 7건을 확인했으나 실행은 0건이다. 선행 focused TestRunner 실행의 씬 객체 identity 변경으로 추가 scene 교체 시험을 보류했다. 실제 공개 도면 기반 실제 역·현장 검증 및 제품 수용 미완료.
2026-09-21: 후속 부산역 공개 안내도와 OSM footprint/platform으로 3층 시각 공간을 실제 생성·저장했다. 확장 도시3845features를 취득했고 도시builder/오픈월드카메라 소스를 추가했다. 최종 도시 화면 검증은 진행 중이며 시각 형상은 게임용 조정이다.
2026-09-21 최신: 1m 도시·공개 DEM, 부산역3층/주요2거점 내부, Blender17종 및 문자형상 제거를 실제 Editor에 저장. 출처/설계 치수 구분, 현장 실측/전체 디지털트윈 수용 아님. 후속4개기관 실제지도위치와미터도시연결,3층바닥두께/패턴/기능구역 native확인. 실제외관·내부전체복원목표로상향했으며현재추정geometry는미검증. 북항2023계획CAD묶음확보는as-built완료가아님.

원문 ID: `CS-WORLD.01.01`, `CS-WORLD.01.02`, `CS-WORLD.02.01`, `CS-WORLD.02.02`, `CS-WORLD.02.03`, `CS-WORLD.03.01`, `CS-WORLD.03.02`, `CS-WORLD.04.01`, `CS-WORLD.04.02`

- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json)
- 증거: [Assets/ChooGuard/World/WorldEntityAnchor.cs](../../Assets/ChooGuard/World/WorldEntityAnchor.cs)
- 증거: [Assets/ChooGuard/Editor/FixtureBuilder.cs](../../Assets/ChooGuard/Editor/FixtureBuilder.cs)
- 증거: [Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0101Tests.cs](../../Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0101Tests.cs)
- 증거: [Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0102Tests.cs](../../Assets/ChooGuard/Tests/EditMode/Stories/CSWORLD0102Tests.cs)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-operations-graph-camera-building.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-operations-graph-camera-building.json)

### native RTS 작업공간 · PARTIAL

content-validation에서 PLAY0101 5/5, PLAY0102 8/8, IntegratedInput PlayMode 12/12. TMP Escape/Enter fixture 경계·EditMode disable lifecycle 및 중간 boundary-retest의 native SendMessage assertion 자동 시험 실패는 해소됐다. 당시 PACK0102 전용 재시험에서 PLAY는 재실행하지 않았다. 주입 fixture 통과는 실제 OS 키보드/IME 수용 검증이 아니므로 PARTIAL 유지. 후속 operations-mailbox-retest에서 PLAY0102 EditMode 8/8 재통과; PlayMode 재실행은 아니다. 이후 별도 independent-playmode의 Bootstrap 1/1·IntegratedInput 12/12 시험 PASS를 확인했으나 ProjectSettings.asset mode 보존 실패로 overall FAIL이다. 실제 OS 입력 수용과 구분한다. 후속 SelectionService/명령 preview controller의 열린 Editor 시험 PLAY0201 3/3·PLAY0202 8/8 PASS. 이후 native host/prefab을 작성해 BCL probe 12건 PASS, native host 시험 3건은 NOT_RUN. 재활성화의 모달 복구 결함 1건을 소스로 확인해 Astra low가 수정 중이다. prefab은 지정 import로 등록됐지만 Editor의 Presenter는 여전히 이전 plain 타입이어서 새 컴포넌트/화면/실제 backend 조립은 미확인이다.
2026-09-21: 한국어 Noto/Lucide 기반 UI와 실제 명령 preview/receipt를 연결했다. 사용자 최우선 레퍼런스 JWE3에 맞춘 full-screen HUD/팀 실행 흐름 후속 구현과 검증 진행 중.
2026-09-21 최신: 한국어UI51텍스트 유지/월드TMP0, 층별cutaway와실제사람축척 렌더 확인. UI앵커/메시GPU업로드 결함수정. 사용자조작성/OS입력 수용은미완료. 기관요청/추적/복귀와현장팀지시가가용상태로연결됐다. 실제UIcallback native검증이며OS키보드/마우스·20~30분게임성수용은남음.

원문 ID: `CS-PLAY.01.01`, `CS-PLAY.01.02`, `CS-PLAY.01.03`, `CS-PLAY.02.01`, `CS-PLAY.02.02`, `CS-PLAY.03.01`, `CS-PLAY.03.02`, `CS-PLAY.03.03`, `CS-PLAY.04.01`, `CS-PLAY.04.02`, `CS-PLAY.05.01`, `CS-PLAY.05.02`, `CS-PLAY.06.01`, `CS-PLAY.06.02`, `CS-PLAY.07.01`

- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-playmode-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-playmode-validation-2026-09-20.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-operations-graph-camera-building.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-operations-graph-camera-building.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-production-benchmark-oss-kernel.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-production-benchmark-oss-kernel.json)

### A/B 운영안 실험 · NOT_STARTED

미착수

원문 ID: `CS-LAB.01.01`, `CS-LAB.01.02`, `CS-LAB.02.01`, `CS-LAB.02.02`, `CS-LAB.03.01`, `CS-LAB.03.02`, `CS-LAB.04.01`, `CS-LAB.04.02`


### 교육·랜덤 실험 모드 · PARTIAL

미착수
2026-09-21: 무작위 참조 화재/군중 실험 director와 JSONL Unity bridge 소스 추가, 최종 live 동작 검증 중. 튜토리얼은 후속 범위, 초안 생성은 MVP 제외.
2026-09-21 최신: 미터씬에서실제워커50명 일시정지초기화확인. NPC평시·사건전환·자원운영루프는비교제안단계.

원문 ID: `CS-MODES.01.01`, `CS-MODES.01.02`, `CS-MODES.02.01`, `CS-MODES.02.02`, `CS-MODES.03.01`, `CS-MODES.03.02`, `CS-MODES.03.03`

- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json)

### 물리·행동 계산 · PARTIAL

미착수
2026-09-21: FDS6.11.1 Rosetta 실제120초 참조 계산, JuPedSim1.4.2 CFSV3 실제 보행 및 FDS장 결합 실행 완료. workers/physics/evidence 참조. 압착 접촉력·임상·부산역 보정·독립 정확도는 미완료.
2026-09-21 최신: 표시좌표를1m로정합했고참조30×20m/보행자1.72m 확인. 물리worker/CFD값변경없음; 새전체피난시험으로간주하지않음. DotRecast현재2층경로native와FDS원본node단면연결검증. SUMO1.27.1별도실제도로사례실행,아직Unity교통모션연동전. FDS0~120s참조범위/현장·압착·임상한계유지.

원문 ID: `CS-SIM.01.01`, `CS-SIM.01.02`, `CS-SIM.01.03`, `CS-SIM.02.01`, `CS-SIM.02.02`, `CS-SIM.03.01`, `CS-SIM.03.02`, `CS-SIM.04.01`, `CS-SIM.04.02`, `CS-SIM.05.01`, `CS-SIM.05.02`, `CS-SIM.05.03`

- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-agency-core-benchmark.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-operations-graph-camera-building.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-operations-graph-camera-building.json)
- 증거: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-production-benchmark-oss-kernel.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-production-benchmark-oss-kernel.json)

### 대본 저작 · NOT_STARTED

미착수

원문 ID: `CS-SCRIPT.01.01`, `CS-SCRIPT.01.02`, `CS-SCRIPT.02.01`, `CS-SCRIPT.02.02`, `CS-SCRIPT.03.01`, `CS-SCRIPT.03.02`, `CS-SCRIPT.04.01`, `CS-SCRIPT.04.02`, `CS-SCRIPT.04.03`


### 사용자·현장 검증 · NOT_STARTED

미착수

원문 ID: `CS-PROOF.01.01`, `CS-PROOF.01.02`, `CS-PROOF.02.01`, `CS-PROOF.02.02`, `CS-PROOF.02.03`, `CS-PROOF.03.01`, `CS-PROOF.03.02`, `CS-PROOF.04.01`, `CS-PROOF.04.02`


### 배포·복구 · NOT_STARTED

미착수

원문 ID: `CS-SHIP.01.01`, `CS-SHIP.01.02`, `CS-SHIP.02.01`, `CS-SHIP.02.02`, `CS-SHIP.03.01`, `CS-SHIP.03.02`


## 최소 개발 절차

- 이번 변경 범위 확인 → Astra low 구현 → 관련 테스트 → state/progress.json 한 곳 갱신. 실제 모델/effort를 확인할 수 없으면 UNKNOWN으로 보고한다.
- v5는 개발 절차만 대체한다. v4 plan.json의 각 ID는 해당 원문 객체 전체를 참조한다. requirementIds, supportsRequirementIds, acceptance(AC), parentProductTestIds(AT), outputs, writes, requires, sourceRefs, specRefs, parentContract, parentSpecRef, requiredReads 및 basis의 제품·안전 계약을 생략하거나 복제하지 않는다.
- 기능영역과 legacy ID는 체크리스트·원문 탐색용이며 활성 children이나 실행 게이트가 아니다. 의존성은 기술 순서와 실제 입력 준비 판단에만 쓰고 과거 prepare/candidate/integration/qualification receipt·parent gate·작업창 승인을 요구하지 않는다.
- 항목별 PLAN/CONTEXT/IMPLEMENTATION/REVIEW 인계 세트, 전원 검수, 자동 재검수 루프, 반복 전체 hash·graph 검사, 작은 항목마다 다음 Story 선택 절차를 제거한다. 필요한 코드 검수는 변경 코드에 한정한다.
- 변하지 않은 환경을 재설치·재탐색·재실행하지 않는다. 문서 보완만으로 Unity를 다시 실행하지 않는다. Windows/현장 입력 부재는 관련 검사만 보류하고 독립 구현을 막지 않는다.
- 완료·대기·미착수 기능을 구분하고 시험 개수를 진척률로 사용하지 않는다. 필수 Windows x64/Mono 개발 빌드·실행 및 원문 제품 수용 기준을 유지한다. 선택적 검사와 과거 절차의 미실행을 새 필수 승인 게이트로 올리지 않는다.

## 제품·안전 경계

- Unity native PC, uGUI/TMP/Input System, TUTORIAL 및 RANDOM_OPERATIONS_LAB 제품 계약을 유지한다. 통합 계획은 구현 존재·기관 승인·안전성을 증명하지 않는다.
- 쓰기 범위는 참조된 v4 writes와 기술 계약 및 이번 사용자 승인 범위로 제한한다. 통합은 무제한 쓰기 권한이 아니다. 공개 층별 도면·지도 근거와 가정을 구별하며 임의 배치로 실제 공간을 대체하지 않는다.
- 설계 변경, 안전/데이터 무결성 문제, 같은 원인의 두 번 실패 또는 범위 충돌 때만 해당 문제의 판단을 요청한다. 단순 수정으로 전체 계획을 다시 작성하지 않는다.
- 기존 v4 진행 장부의 RELEASED claim 및 빈 수용 기록, Bootstrap 인계·원시 증거는 역사로 보존한다. Bootstrap의 IMPLEMENTED_NOT_VERIFIED는 해당 부분에만 적용한다.
- 이번 관리 전환은 제품 기능 착수나 ACCEPTED 설정이 아니다. worktree·commit·push·PR·외부 게시·설치·graph 재생성을 승인하지 않는다.

## 기록된 시험

시험 수를 제품 진척률로 해석하지 않는다. 과거 증거와 신규 실행을 구분한다.

- **Bootstrap EditMode — PASS**: 51/51
  - 범위: CS-BOOT.01.01 only; 과거 로컬 실행 재사용, 관리 전환에서 재실행 안 함
  - 결과: [docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/editmode-final/results.xml](../../docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/editmode-final/results.xml)
- **Bootstrap PlayMode — PASS**: 1/1
  - 범위: CS-BOOT.01.01 only; 과거 로컬 실행 재사용
  - 결과: [docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/playmode-final/results.xml](../../docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/playmode-final/results.xml)
- **Bootstrap strict baseline — PASS**: 12/12
  - 범위: CS-BOOT.01.01 only; 구조 검사이지 Windows 제품 수용 아님
  - 결과: [docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/baseline-final-validation.json](../../docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/baseline-final-validation.json)
- **Windows x64/Mono 개발 빌드·실행 — NOT_RUN**: Windows 모듈/host 부재. 자동 설치·대체 타깃 실행 없음.
  - 범위: CS-BOOT.01.01 only
  - 결과: [docs/build/evidence/CS-BOOT.01.01/20260920T0252168282370Z-0f57d6/build-receipt.json](../../docs/build/evidence/CS-BOOT.01.01/20260920T0252168282370Z-0f57d6/build-receipt.json)
- **Asset and input EditMode integration — FAIL**: 144 total / 141 pass / 3 fail / 0 skip; exit 2
  - 범위: 7 whole classes; required scratch fixtures supplied
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json)
- **Asset and input PlayMode integration — FAIL**: 11 total / 9 pass / 2 fail / 0 skip; exit 2
  - 범위: 2 whole classes; temporary batch focus settings restored
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json)
- **Boundary retest EditMode — FAIL**: 148 total / 140 pass / 8 fail / 0 skip / 0 inconclusive; exit 2; 실패 전부 CSPLAY0102 native SendMessage ShouldRunBehaviour assertion
  - 범위: 7 whole classes; unity-import 이후 중간 실행, 후속 content-validation에서 입력 실패 해소
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json)
- **Boundary retest PlayMode — PASS**: 12/12; Bootstrap 1/1, IntegratedInput 11/11; 0 skip / 0 inconclusive; exit 0
  - 범위: 2 whole classes; unity-import PlayMode 실패 후속 실행; 실제 OS 입력 수용 아님
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json)
- **Content validation EditMode — FAIL**: 230 total / 229 pass / 1 fail; exit 2; BOOT0101 55/55, BOOT0201 44/44, BOOT0202 17/17, PACK0101 14/14, PACK0102 79/80, PLAY0101 5/5, PLAY0102 8/8, PreparedAssetImport 7/7. 유일 실패는 malformed 최대 revision fixture; compiler errors 0
  - 범위: 8 whole classes; boundary-retest BOOT/PLAY 자동 실패 해소, PACK0102 fixture 실패는 뒤의 좁은 실행에서 해소
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json)
- **Content validation PlayMode — PASS**: 13/13; Bootstrap 1/1, IntegratedInput 12/12; exit 0; compiler errors 0
  - 범위: 2 whole classes; 주입 fixture 기반 입력 회귀, 실제 OS 키보드/IME·Windows·제품 수용 아님
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json)
- **Content revision fixture retest EditMode — PASS**: 83/83; revision 독립 조합 (0,0), (max,max), (0,max), (max,0), (2,9) 5개 포함; max=9223372036854775807; failed/skip/inconclusive 0, exit 0, compiler errors 0
  - 범위: CSPACK0102Tests only; 앞선 최대 revision fixture 오류 수정 후 좁은 재시험. 다른 통과 EditMode/PlayMode suite는 재실행하지 않음; 전체 246건 단일 green 실행 아님
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json)
- **Synthetic two-agency validation EditMode — PASS**: 153/153 leaf PASS; PACK0101 14/14, PACK0102 83/83, PACK0103 56/56; failed/skipped/inconclusive 0, compiler errors 0, exit 0; source freeze 일치 및 protected 304 files unchanged는 해당 실행 증거 기준.
  - 범위: CSPACK0101Tests/CSPACK0102Tests/CSPACK0103Tests 단일 실행; synthetic fixture 및 trusted test-only projection·관계 검증. Bootstrap·PlayMode·무관 suite 재실행 없음; 전체 green run·생산 운영 실행·현장/Windows 수용 아님. 기존 실행 증거 기록이며 문서 갱신에서 Unity 재실행 안 함; provider UNKNOWN.
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/two-agency-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/two-agency-validation-2026-09-20.json)
- **Operations mailbox initial validation — NOT_RUN**: Root runner 쓰기 제한과 기존 Bootstrap Windows NOT_RUN receipt 출력 경로 충돌로 Unity 미실행. 후속 operations-mailbox-retest에서 허용된 신규 receipt 1개 범위로 충돌 해소; 최초 BLOCKED 증거는 보존.
  - 범위: 8 whole classes 검증 준비 단계에서 BLOCKED; Unity 0회, 소스/시험 실패 아님; provider UNKNOWN.
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-validation-2026-09-20.json)
- **Operations mailbox retest EditMode — PASS**: 300/300 leaf Passed; OPS0101 21/21, BOOT0101 55/55, BOOT0201 46/46, BOOT0202 17/17, PACK0101 14/14, PACK0102 83/83, PACK0103 56/56, PLAY0102 EditMode 8/8. failed/skipped/inconclusive/compiler errors 0, exit 0, timeout 없음. OPS 동시 쓰기·결정적 정렬/비교 경계·처리 sequence 역전·성공 prefix/실패+후속 retry·count overflow·sequence exhaustion 및 emitted pure DLL oracle Passed. 해당 실행 evidence 기준 source freeze 8/8 일치, 기존 2210 files 변경·삭제 0, meta 166 보존, 예상 밖 신규 0. 허용된 신규 Windows NOT_RUN receipt 1개는 Windows 빌드 통과 증거가 아니다.
  - 범위: Unity batchmode EditMode 1회, 8 whole classes 한정. PlayMode/Windows/실제 OS 입력/현장/전체 제품 수용 미실행; 전체 suite green 아님. 기존 실행 증거 기록이며 문서 갱신에서 Unity 재실행 안 함; provider UNKNOWN.
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/operations-mailbox-retest-2026-09-20.json)
- **Independent foundations EditMode validation — FAIL**: 시험 431/431 PASS, failed/skipped/inconclusive 0, compiler errors 0, exit 0. GraphicsSettings.asset·ShaderGraphSettings.asset mode 0644→0600 보존 실패로 overall FAIL. 두 파일 bytes/hash 동일, 행위자 UNKNOWN. 후속 World preview 수정의 compile/tests 증거 아님.
  - 범위: 좁힌 독립 foundations 14 whole classes; World0101/0102 및 fixture NOT_RUN, PreparedAssetImport 제외. 별도 PlayMode와 합산한 단일 green 실행 아님; 기존 증거만 기록, provider UNKNOWN.
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json)
- **Independent PlayMode validation — FAIL**: 시험 13/13 PASS(Bootstrap 1/1, IntegratedInput 12/12), failed/skipped/inconclusive 0, compiler errors 0, exit 0. ProjectSettings.asset mode 0644→0600 보존 실패로 overall FAIL. bytes/hash 동일, 행위자 UNKNOWN. 과거 FAIL을 대체·삭제하지 않는다.
  - 범위: CSBOOT0101PlayModeTests·IntegratedInputPlayModeTests 두 whole classes 별도 실행. EditMode/World fixture/실제 OS 입력/Windows/현장 수용 아님; Jev 검수에서 이 후속 실행은 미평가. provider UNKNOWN.
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-playmode-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-playmode-validation-2026-09-20.json)
- **World preview lifecycle compile and tests — NOT_RUN**: Unity compile/EditMode tests 미실행. saved fixture 생성·SaveScene 성공·reload 동등성·실제 scene 보존 및 persistence 수용 NOT_VERIFIED.
  - 범위: FixtureBuilder 및 CSWORLD0101/0102 후속 preview lifecycle 수정; 기존 독립 foundations와 Jev World snapshot 이후.
  - 결과: 미실행; 결과 파일 없음
- **Physics runtime and initial native MVP — PASS**: 저장 probe7 PASS; FDS120s completed; real JPS crowd72/fire24 completed; 전체 제품/부산역물리/최종HUD PASS 아님
  - 범위: 각각 별도 실행한 열린 Editor 저장 probe7건 및 FDS/JPS 참조 사례의 실행 성공에 한정. 통합 green run·실제 부산역 검증 아님
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-physics-openworld-mvp.json)
- **Direct-command native client to solver flow — PASS**: 일시정지 계획, 개별 집단 유도 및 대기 ACK, 전체 대피 후69/69명54초 완료. 별도 animation repair는 실제 보행 pose변화 및 interaction시간재시작 PASS.
  - 범위: 공식 Unity CLI를 통한 UI callback 및 실제 worker 연동. OS 마우스/키보드·시각 품질·전체 부산역 검증 아님
  - 결과: [.planning/2026-09-20-integrated-build/live-reposition-flow-guarded.json](../../.planning/2026-09-20-integrated-build/live-reposition-flow-guarded.json)
- **Metre scene and UI-only text native check — PASS**: scene saved clean; root/source scale1; crew~1.78m; worldTMP0/UI51; ref30×20m; currentcompiler0
  - 범위: 실제 Editor 저장 및 메시/글자/인물치수; 실측역사·상용시각수용 아님
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json)
- **Metre paused worker and rendered agents — PASS**: actual worker ready,failedfalse,pausedtrue,sim0s,50명;50agent unionheight1.71985m
  - 범위: 일시정지 초기화와 화면 연동만; 완주/피난안전/임상판정 아님
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json)
- **Production visual and field twin acceptance — NOT_RUN**: 기관도시출동/영속시민/현장작업/복구의부분구현과native증거존재. 전체상용품질·현장보정·압착·임상수용은미완료.
  - 범위: JWE3급프로덕션품질·사용자수용·부산역현장검증
  - 결과: [docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-21-world-metre-npc-proposal.json)
- **Official guide review — PASS**: 15guidepages catalogued,14bodytexts+welcomeoverview,52HowToanchors,help/controls/2toolboxes;wholewelcomevideo notviewed
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/coverage.json](../../.planning/2026-09-21-official-guide-benchmark/coverage.json)
- **Four source agency points — PASS**: Nativepoints match sourcecatalog projection, notsurveyedentranceaccuracy
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/agency-coordinate-native-parity.json](../../.planning/2026-09-21-official-guide-benchmark/agency-coordinate-native-parity.json)
- **Central119 actual city roundtrip — PASS**: PausedUIrequest→directedroad→135ssimarrival→onsiteownACK170s→return→343sbaseReady;duplicate/busy/unavailablegates
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/agency-native-proof.json](../../.planning/2026-09-21-official-guide-benchmark/agency-native-proof.json)
- **Current 2F path query — PASS**: NativeDetour obstacleavoidance,3stagingtargetsreachable,unsupportedfloorrejected
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/nav-query-source-validation.json](../../.planning/2026-09-21-official-guide-benchmark/nav-query-source-validation.json)
- **Current 50-person evacuation and Recovery — PASS**: Currentnav+actualworker50exited67simseconds;separate runfromagencytrip
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/nav-evac-native-proof.json](../../.planning/2026-09-21-official-guide-benchmark/nav-evac-native-proof.json)
- **Corrected Ready UI — PASS**: Unavailablecrewcard showsagency;onsitebuttonsdisabled;emptyfirepanelhidden
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/ready-hud-native-proof.json](../../.planning/2026-09-21-official-guide-benchmark/ready-hud-native-proof.json)
- **SUMO installed and used — PASS**: ProjectlocalmacOSarm64SUMO1.27.1;fullOSMnetconvert/duarouter/SUMOexit0;one169s/1330.3mreferencevehicle;UnityvehiclemotionnotSUMO-driven
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/sumo-tooling-graph.json](../../.planning/2026-09-21-official-guide-benchmark/sumo-tooling-graph.json)
- **Agency-first context — PASS**: 실제도시시작→현장보기2F→기관위치도시 UIcallback검증,기관이도시의주패널로표시됨
  - 범위: 한정된 현재 로컬 증거; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-official-guide-benchmark/agency-context-native-proof.json](../../.planning/2026-09-21-official-guide-benchmark/agency-context-native-proof.json)
- **Executable operations graph — PASS**: 12nodes19edges,3jobs,50evacuated,all3returnedReady475simsec;56provenance records
  - 범위: 현재 로컬 구현의 한정된 검증; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-fleet-building/progression-native-provenance-proof.json](../../.planning/2026-09-21-fleet-building/progression-native-provenance-proof.json)
- **Camera native API seams — PASS**: cursoranchor<1px,focus/follow/cancel/floor/DEM;OSmouse andbackgroundqueuedinput excluded
  - 범위: 현재 로컬 구현의 한정된 검증; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-fleet-building/camera-native-proof.json](../../.planning/2026-09-21-fleet-building/camera-native-proof.json)
- **Restored building height/native refs — PASS**: 3FBXs rendered;95numeric tags preserved incl200m;existingfloors/navrefs
  - 범위: 현재 로컬 구현의 한정된 검증; 전체 제품 수용 아님
  - 결과: [.planning/2026-09-21-fleet-building/building-height-native-proof.json](../../.planning/2026-09-21-fleet-building/building-height-native-proof.json)
- **Contextual building game UI — PASS**: Defaultnocard,selectedbuildingrequests,targetclick,inside-onlyfloorcontrols,nopublicAI/graphUI
  - 범위: Native world-ray andactualbuttoncallbacks; notOSinput/JWE3parity
  - 결과: [.planning/2026-09-21-production-benchmark/context-ui-native-proof.json](../../.planning/2026-09-21-production-benchmark/context-ui-native-proof.json)
- **OpenRA countdown and internal Jev arithmetic — PASS**: SourceWaitported;0.55→28.5s→29accepteds;pausefreeze/unavailableuntilready;allteamsReady443s
  - 범위: Actualtwo-teamlocalreferenceoperation; notcalibratedphysicalor119timings
  - 결과: [.planning/2026-09-21-production-benchmark/turnaround-native-proof.json](../../.planning/2026-09-21-production-benchmark/turnaround-native-proof.json)

## 실패·보류·미착수

- **WINDOWS_ACCEPTANCE · OPEN · 필수 수용 관련** — CS-BOOT.01.01 및 적용되는 제품 수용: Windows x64/Mono 개발 빌드·실행 미실시. 가능한 독립 구현을 막지 않는다.
  - [docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json](../../docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json)
- **BOOTSTRAP_MANUAL_ACCEPTANCE · OPEN · 필수 수용 관련** — CS-BOOT.01.01: Player/수동 검사와 OFL 배포 확인 미완료. 기존 부분 로컬 증거를 전체 수용으로 승격하지 않는다.
  - [docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json](../../docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json)
- **PRODUCT_REMAINDER · OPEN · 필수 수용 관련** — 11개 기능영역의 부분 구현 외 나머지 요구: 부분 구현 영역을 제외한 나머지 구현·통합·현장/지정 용도 검증 미착수. 실제 입력 부재는 해당 검증만 보류한다. 6 foundation의 guard/planner/catalog/validator/anchor 구현은 전체 업무 통합 완료가 아니다. 본업무 port/store·durable commit·outbox·projection blob resolver 및 World preview 수정 후 실행 검증·saved fixture/save/reload 검증이 남아 있다. 독립 EditMode/PlayMode의 시험 PASS와 설정 mode 보존 실패(overall FAIL)를 분리하며 행위자는 UNKNOWN이다. 실제 역 검증도 미완료다. Jev 6건 escalate는 advisory 결과로 확정 버그 6건이나 신규 승인 게이트가 아니다.
  - [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-foundations-validation-2026-09-20.json)
  - [docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-playmode-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/independent-playmode-validation-2026-09-20.json)
- **UNITY_IMPORT_INPUT_REMAINDER · OPEN · 필수 수용 관련** — BOOT/PACK/PLAY integration: 해소된 자동 시험 실패: 기존 OTF font whitelist·순수 모듈 compiler reference·EditMode router disable lifecycle·TMP Escape/Enter fixture 경계, 중간 boundary-retest의 CSPLAY0102 native SendMessage assertion 8건은 content-validation의 BOOT/PLAY 통과로 후속 확인됐다. content-validation의 PACK0102 최대 revision fixture 오류 1건은 PACK0102 전용 83/83 재시험으로 해소됐다. 과거 FAIL 기록은 보존하며 이 서로 다른 실행을 전체 단일 green 재실행으로 합산하지 않는다. 남은 NOT_RUN: Windows x64/Mono 실제 빌드·실행, 실제 OS 키보드/IME, FBX animation playback, audio listening, measured-site qualification. 주입 자동 시험·build seam simulation은 물리 입력/Player/현장 수용 증거가 아니므로 이 항목은 OPEN 유지하며 독립 개발을 막지 않는다.
  - [docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/unity-import-2026-09-20.json)
  - [docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/boundary-retest-2026-09-20.json)
  - [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-validation-2026-09-20.json)
  - [docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json](../../docs/CHOOGuard_Story_Plan_v5/state/evidence/content-revision-retest-2026-09-20.json)

## 보존된 과거 기록

- [docs/CHOOGuard_Story_Plan_v4/state/progress.json](../../docs/CHOOGuard_Story_Plan_v4/state/progress.json)
- [docs/agent-handoffs/CS-BOOT.01.01/IMPLEMENTATION.json](../../docs/agent-handoffs/CS-BOOT.01.01/IMPLEMENTATION.json)
- [docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json](../../docs/agent-handoffs/CS-BOOT.01.01/REVIEW.json)

## 사용

기본 반복은 `prompts/02-implement-story-low.md` 하나다. JSON 진행 기록을 바꾼 뒤 다음 명령으로 이 뷰를 갱신한다.

```sh
python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py validate
python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py brief
python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py render
python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py render --check
```
