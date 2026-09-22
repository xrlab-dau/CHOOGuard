# 아키텍처 의사결정 기록

상태: 설계 선택. 설치·컴파일·제품 수용은 별도다. 재검토 조건은 사용자 요구와 검증 증거에 따른다.

## ADR-01 · 로컬 모듈형 코어 + 격리 워커

**선택:** 한 PC에서 UI·운영·저장이 함께 실행되고 무거운 solver만 child process로 분리한다.

**대안·트레이드오프:** 전면 마이크로서비스/브로커를 반려: 배포·운영 복잡도가 최초 가치 검증을 압도한다.

**재검토:** 다수 현장·공유 작성의 실측 병목과 보안 요구가 확인될 때 remote adapter를 추가한다.

## ADR-02 · uGUI/TMP native-only

**선택:** 주 화면은 실제 Unity WorldCamera+HUD. Input System은 현행 dependency와 spike 후 적용.

**대안·트레이드오프:** HTML/WebView 및 이중 UI 프레임워크를 기본에서 제외.

**재검토:** 실제 native 접근성/성능 근거로만 UI 프레임워크 전환 ADR을 검토한다.

## ADR-03 · 단일 운영 소유자

**선택:** run별 state mutation을 하나의 serialized owner가 수행한다.

**대안·트레이드오프:** 여러 worker의 shared-memory 직접 변경 및 UI callback의 상태 변경 금지.

**재검토:** 배치 스케일아웃은 run별 partition부터, 한 run의 동시 writer 확대는 별도 검증.

## ADR-04 · 범위를 제한한 event sourcing

**선택:** 실행 ledger·계획/리뷰 revision은 불변, 설정·인덱스는 일반 mutable storage.

**대안·트레이드오프:** 모든 CRUD를 이벤트로 만들지 않음.

**재검토:** 재현·감사 필요성과 저장비를 실제로 측정해 확대한다.

## ADR-05 · 로컬 SQLite + blob store

**선택:** 한 writer·durable acknowledgement, large arrays는 content-addressed files.

**대안·트레이드오프:** 네트워크 공유 DB·큰 격자장의 JSON row 저장·필수 원격 DB 반려.

**재검토:** 동시 작성·대형 조직 정책이 필요하면 서버 저장 adapter를 추가하되 semantic commit 동일.

## ADR-06 · 길이-prefix pipe IPC

**선택:** 작은 JSON control+immutable numeric files, stdout/log 분리.

**대안·트레이드오프:** Unity에 최신 .NET/gRPC·Python runtime을 그대로 강제 임베딩하지 않음.

**재검토:** 측정된 IPC 병목이 있을 때 binary transport/shared memory를 도입하고 zero-copy 수명 검증.

## ADR-07 · capability 기반 co-simulation

**선택:** 필수 worker의 같은 경계 결과만 commit, restore/replay 경로 명시.

**대안·트레이드오프:** 모든 solver의 공통 Step/rollback을 가정하지 않음.

**재검토:** 실제 adapter의 기능·오차·restart 검증으로 경로를 승격한다.

## ADR-08 · FDS는 기준 계산, 가속은 검증 후

**선택:** FDS batch reference와 stateful ROM의 주장 범위를 분리.

**대안·트레이드오프:** 실시간 smoke VFX 또는 사전 영상 전환을 물리 계산으로 인정하지 않음.

**재검토:** target use case의 시간·정확도·conservation·OOD·checkpoint 통과 후 가속 선택.

## ADR-09 · 완전 checkpoint와 공정 비교

**선택:** 메시지/예약/worker/RNG/입력 lock과 부모 cutoff를 보존한다.

**대안·트레이드오프:** seed와 위치만 저장하는 게임 세이브 또는 A의 내생 이벤트 복사 반려.

**재검토:** replay 불가능 시 정확 분기 비활성화, 새 실행으로 분류한다.

## ADR-10 · 매뉴얼은 검수된 RuleIR

**선택:** LLM 추출 후보와 적용 승인 별도; 기관/관할/판본/예외 결속.

**대안·트레이드오프:** 원문 문장 전체를 hard lock으로 자동 변환하지 않음.

**재검토:** 검수자·적용 자료 확보 시 rule bundle을 versioned release한다.

## ADR-11 · AI는 제안자, 실행자는 코어

**선택:** 구조화 근거 pack→제안→검사→사람 결정. 승인·계산값 자유 생성 금지.

**대안·트레이드오프:** unbounded autonomous agent가 현장 명령·shell 실행하도록 하지 않음.

**재검토:** 동일 evidence eval에서 도움이 입증된 도구만 allowlist로 확장한다.

## ADR-12 · 검색은 작은 근거 범위부터

**선택:** 판본/기관 필터와 전문검색, 필요한 graph traversal.

**대안·트레이드오프:** 전면 GraphRAG·별도 graph DB를 SOTA라는 이유만으로 필수화하지 않음.

**재검토:** 한국어 query set에서 recall·provenance 이득을 검증 후 embedding/reranker 추가.

## ADR-13 · 현실/가상/실행/대본 정본 분리

**선택:** SiteBundle, Scenario+Plan, Run, ScriptIR를 별도 revision으로 관리.

**대안·트레이드오프:** 대본 수정이 원시 로그를 바꾸거나 현장 상태를 바꾸지 않음.

**재검토:** migration은 새로운 revision과 영향 검토를 남긴다.

## ADR-14 · 기존 Foundation 점진 이행

**선택:** 기존 command·observation·checkpoint adapter를 재사용, 현장 근접행동과 RTS author 분리.

**대안·트레이드오프:** 과거 구현 제거·새 엔진 전면 재작성·두 authority 동시 적용 반려.

**재검토:** baseline regression과 shadow comparison 후 run 단위 cutover.

## ADR-15 · 검증은 다축·주장 단위

**선택:** 계약/코어/Unity/모델/사용자/기관을 별도 판정.

**대안·트레이드오프:** 문서 테스트 수나 그래픽으로 안전·시장성을 승인하지 않음.

**재검토:** 독립 증거와 명시된 scope가 생겼을 때만 해당 facet를 승격한다.

## ADR-16 · 무료 시각 자산과 물리 데이터 분리

**선택:** 무료판 범위·bytes·부품·rig·성능 검사 후 입고, 계산 형상 별도 근거.

**대안·트레이드오프:** 게임-ready 마케팅 문구에서 실제 치수·능력 추론 금지.

**재검토:** 실측/대체 모델이 필요하면 해당 객체만 보완하고 운영 식별자 유지.
