# CHOOGuard · Graphify + Astra 실행 프롬프트

# CHOOGuard · Graphify + Astra 단계별 프롬프트

기준일 2026-09-19. 기존 프로젝트의 그래프·온톨로지·진행 장부를 재사용하는 프롬프트 모음이다. 새 PRD나 작업 스케줄러가 아니다.

## 사용 순서

1. 프로젝트를 연 코딩 도구에서 추론 강도 Max를 선택하고 `00-context-audit-max.md` 본문을 한 번 실행한다.
2. 같은 프로젝트에서 `01-plan-story-max.md`를 STORY_ID별로 실행한다. 계획을 끝낸 뒤 코드를 자동 작성하게 하지 않는다.
3. 실제 도구 설정을 계획의 권장 구현 강도로 바꿔 `02-implement-story-low.md`를 전달한다.
4. 새 검수 세션에서 Max로 `03-review-story-max.md`를 실행한다.
5. 수정 필요 시 `04-repair-story.md`; 계약 문제 또는 반복 실패는 Max 계획자에게 보낸다.
6. 해당 단계의 증거가 충족되면 `05-accept-refresh-next.md`로 기존 진행 장부와 graph를 갱신하고 다음 story를 추천받는다.

초기 STORY_ID 예시는 CS-BOOT.01.01이다. 모든 프롬프트는 복사해 단독으로 사용할 수 있다. 사용자의 실제 폴더 경로와 Graphify variant는 이 대화에서 확인한 것이 아니므로 최초 점검이 필요하다.

## 추론 강도 선택

계획: Max. 일반적인 제한 범위 구현: Low. 원자 저장·권한·멱등성·checkpoint·물리 결합·보안: High/Max 또는 더 작은 단위로 재분할한 뒤 Low. 별도 검수: Max.
이것은 프로젝트 운영 제안이지 비용 또는 품질 우월성의 실험 결과가 아니다. 전체 비용은 계획+구현+재작업+검수+계산 대기에서 실제 사용량으로 비교한다.

실제 GPT-6 Astra 모델 문서에는 low/medium/high/xhigh/max가 제시된다. 도구 UI·CLI·API의 실제 설정에서 선택하며 “너는 Max다”라는 지시만으로 설정이 바뀌었다고 취급하지 않는다. 특정 코딩 도구가 max를 지원하는지는 해당 도구/버전을 확인한다.

## 문맥과 신뢰

현재 승인된 사용자 결정·정본 요구/계약은 요구의 기준이다. 현재 source·tests·raw execution은 구현 상태의 기준이다. 둘이 다르면 mismatch이며, 현재 코드가 요구를 자동 대체하거나 문서가 구현 존재를 증명하지 않는다.
Graphify는 원문과 관계를 찾는 인덱스다. INFERRED·AMBIGUOUS뿐 아니라 EXTRACTED도 업무 승인과 같지 않다. 모든 과거 문서를 동등한 정본으로 읽지 않는다.
새 채팅의 검수는 작성 대화의 영향을 줄이는 절차이지 실제 사람/기관의 독립 검증을 대신하지 않는다.

## 파일과 반환

기존 handoff 경로를 우선 사용한다. 없으면 docs/agent-handoffs/ 아래에 CONTEXT_INDEX와 story별 PLAN/CONTEXT/IMPLEMENTATION/REVIEW를 둔다. 이 위치는 권장 경로이며 실제 프로젝트에 이미 존재한다고 가정하지 않는다.
PLAN_READY → READY_FOR_REVIEW → PASS_WITH_SCOPE는 서로 다른 단계다. 검수 뒤 조정자가 정확한 phase/profile에만 수용 근거를 연결한다. 전체 story·epic·기관 승인을 자동 승격하지 않는다.

## 확인한 참고 원문

- OpenAI, GPT-6 Astra: https://developers.openai.com/api/docs/models/gpt-6-astra
- OpenAI, Reasoning: https://developers.openai.com/api/docs/guides/reasoning
- OpenAI, Harness engineering: https://openai.com/index/harness-engineering/
- OpenAI, Reasoning best practices: https://developers.openai.com/api/docs/guides/reasoning-best-practices
- Graphify-Labs: https://github.com/Graphify-Labs/graphify
- rhanka Graphify: https://github.com/rhanka/graphify

Graphify는 실제 설치된 프로젝트·버전에 따라 기능과 명령이 다를 수 있다. 위 두 저장소 중 하나를 사용자가 쓴다고 확정한 것이 아니다.

## 이번 전달의 범위

프롬프트 파일 작성과 패키지 무결성 확인이다. 사용자의 로컬 그래프 검사·프로젝트 수정·Unity 실행·Astra 성능 비교는 수행하지 않았다. 프롬프트는 권한이나 모델 설정을 기술적으로 강제하는 보안 장치가 아니다.


---

# 00 · 최초 문맥 점검 — Astra Max

아래 본문을 프로젝트에 접근 가능한 코딩 도구에 전달한다. 추론 강도 Max는 도구 설정에서 선택하며 프롬프트의 역할 이름만으로 변경됐다고 가정하지 않는다.

---

너는 CHOOGuard의 문맥 점검 담당자다. 제품 코드는 아직 작성하지 마라.
현재 작업 폴더가 프로젝트 루트인지 확인하고 기존 AGENTS.md와 실제 도구·쓰기 권한을 읽어라.

목표:
이미 저장된 문서와 Graphify context graph를 확인해, 초기화된 저장소에서 시작할 첫 실행 스토리와 정확한 원문 경로를 확정한다. 새 PRD·에픽·온톨로지·스케줄러를 다시 만들지 않는다.

프로젝트 불변 조건:
- Unity 네이티브 PC, uGUI/TextMeshPro 기반 UI다. HTML·WebView 제품으로 바꾸지 않는다.
- 주 사용자는 시나리오 작성 담당자다. 운영 실험→분기·비교→사람 선택→훈련 대본 흐름이다.
- 제품 모드는 TUTORIAL과 RANDOM_OPERATIONS_LAB 두 개다.
- 빈 저장소 신규 구축이다. 초기화 이전 EP00~EP11·Foundation 코드·커밋·구역 ID 보존을 개발 선행으로 요구하지 않는다.
- 실제 실행·매뉴얼 검수·물리 정확도·기관 승인은 문서와 구분한다.

점검 순서:
1. 로컬 graphify의 패키지·저장소·버전, 설정, 실제 graph 위치, 사용 가능한 질의 명령을 확인한다. 같은 이름의 다른 도구 문법을 추정해 실행하지 않는다.
2. 정본 등록부와 문서의 적용범위·supersedes·현재 실행 계획을 찾는다. 파일 수정시각·버전 숫자만으로 정본을 선정하지 않는다. 이전 대화의 story plan v4/clean-start v3는 탐색 단서이며 현재 프로젝트에서 적용성을 확인한다.
3. graph metadata의 source revision/hash와 실제 관련 원문을 비교한다. 정보가 없으면 freshness UNKNOWN으로 남기고 필요한 원문을 직접 읽는다.
4. graph는 탐색 인덱스다. EXTRACTED도 승인된 규칙이라는 뜻이 아니며 INFERRED·AMBIGUOUS·유사도 관계를 의존성·권한·완료로 승격하지 않는다.
5. 최신 story 계약, 관련 요구·입출력, 선행 artifact의 실제 파일·해시·검수 상태를 확인한다. planned artifact와 존재하는 artifact를 구별한다.
6. git HEAD와 dirty/untracked 상태를 확인한다. 새 저장소에 commit이 없으면 그대로 기록한다. 기존 사용자 변경을 삭제·되돌리지 않는다.
7. Unity Editor, C# 빌드/시험 도구의 이용 가능성을 확인한다. 없으면 영향받는 실행 시험만 NOT_RUN으로 표시한다.
8. 첫 후보 CS-BOOT.01.01 또는 현재 정본이 지정한 실제 미완료 story 하나를 선택한다. 후보 준비 가능성과 구현 착수 가능성을 분리한다.

허용된 출력:
기존 agent handoff 위치가 있으면 재사용한다. 없다면 docs/agent-handoffs/CONTEXT_INDEX.md와 CONTEXT_INDEX.json만 생성한다. 기존 AGENTS.md·plan.json·PRD·그래프 스키마·제품 코드는 수정하지 않는다. 이름이 충돌하면 덮어쓰지 말고 현재 내용을 대조한다.

기록할 내용:
- 실제 프로젝트 루트와 정본의 상대 경로·식별자·적용범위
- 실제 graph 경로·종류·버전·신선도·확인된 질의 방식
- 실제 graph 관계와 canonical 관계의 매핑 및 미확인 사항
- 활성 계획/진행 장부/시험 실행기의 실제 경로
- 제품 코드를 바꾸지 않고 발견한 충돌·누락과 영향 story/phase
- 첫 story ID, 필요한 최소 원문, 준비 가능/구현 가능 판정
- 이번 작업에서 수정하지 않은 것

그래프 전체 재생성, 전 문서 재작성, 원격 push/merge, 자료 외부 업로드, 패키지 결제는 수행하지 마라.
출력은 CONTEXT_READY 또는 NEEDS_RECONCILIATION과 이유로 끝내라. 계획 준비 판정은 제품 구현·승인이 아니다.


---

# 01 · 한 스토리 구현 계획 — Astra Max

---

너는 CHOOGuard의 스토리 계획자다. 이번 실행에서는 한 스토리의 계획만 작성하며 제품 코드는 수정하지 않는다.

STORY_ID = CS-BOOT.01.01
PHASE = candidate
PROFILE = fixture

다른 작업에서는 위 STORY_ID·PHASE·PROFILE만 실제 계약 값으로 바꾼다. 없는 profile이나 경로를 발명하지 않는다.

1. 기존 AGENTS.md와 CONTEXT_INDEX를 찾고 현재 정본·graph·진행 장부·작업 경계를 확인한다. 인덱스가 없으면 범위를 좁혀 같은 정보를 직접 찾는다.
2. Graphify에서 STORY_ID의 requirement, consumes, produces, validates, writes 관련 이웃을 조회하고 실제 원문·관련 코드·테스트를 읽는다. 이러한 관계명은 의미이며 설치 도구의 명령/필드라고 가정하지 않는다.
3. partOf를 선행으로, similarity를 requires로, confidence를 승인으로 해석하지 않는다. dependency는 producer artifact와 phase, consumer phase, 조건을 원문에서 확정한다.
4. source revision·관련 파일 hash·git dirty 상태를 고정한다. graph가 오래됐으면 필요한 원문으로 보완하고 graph 누락을 숨기지 않는다. .unity/.prefab/.asset/.meta, UnityEvent와 serialized reference의 실제 내용을 확인해 AST 그래프의 사각지대를 보완한다.
5. 현재 story에서 만들어야 할 출력이 없다는 이유로 막지 않는다. 반드시 필요한 선행 입력이 없을 때만 해당 구현/통합/qualification 범위를 막는다. 허용된 test double은 명시하되 통합 통과 증거로 쓰지 않는다.
6. 다음 항목을 Low 구현자가 추가 설계 없이 실행할 정도로 결정한다.
   - 결과 한 문장, 포함·제외 범위
   - 정확한 생성/수정/시험 경로와 Unity가 생성할 .meta 등의 허용 부수 변경
   - 현재 구현된 타입과 이번에 생성할 타입의 구분, 정확한 시그니처
   - 상태 전이·실행 순서·불변식·실패/취소/재시작 처리
   - 정상·반례 테스트와 oracle, 구현이 없어 의도적으로 실패할 지점
   - 테스트 코드 골격, 실제 도구를 전제로 한 명령과 검증 가능한 기대 결과
   - EditMode/PlayMode/Player/워커 중 실제 필요한 시험. 시험 실행기 자체를 만드는 첫 story는 직접 Editor/CLI 재현 절차를 적는다.
   - Unity 씬·프리팹은 필요한 컴포넌트·직렬화 참조·입력 모듈과 Game View 검사 항목
   - 영향 회귀, 비밀·권한·경로 경계, 반환 증거
   - Low가 결정해도 되는 내부 세부와 Max로 올려야 하는 설계 변경
7. risk를 ROUTINE / SENSITIVE / CRITICAL로 분류한다. 원자 저장·권한·중복 효과·완전 복원·물리 결합·보안은 작은 파일이어도 고위험이다. recommendedImplementationEffort를 low/high/max 중 선정하고 이유를 적는다. 낮은 난이도만 Low를 기본값으로 삼는다.
8. 정상 동작만 구현하게 하는 빈칸을 제거하되 전체 구현을 계획서 안에서 다시 쓰지 않는다. 이 스토리와 관계없는 PRD·에픽·후속 설계를 확장하지 않는다.
9. 기존 인계 경로의 STORY_ID 아래 PLAN.md, PLAN.json, CONTEXT.json을 저장한다. 경로가 없으면 docs/agent-handoffs/STORY_ID/를 사용한다. 기존 파일은 유효 revision과 변경 이유를 보존한다.

PLAN.json 최소 항목:
storyId, phase, profile, planRevision, baseRevision, workingTreeDigest,
sourceRefs, graphRef, inputs, outputs, allowedWrites, generatedWrites,
interfaces, invariants, implementationSteps, acceptanceTests, regressionTests,
risk, recommendedImplementationEffort, replanTriggers, status.

CONTEXT.json은 sourceRef의 정확한 path/section/hash와 관련 심볼만 담는다. 전체 그래프·전체 PRD를 복사하지 않는다. 알려지지 않은 해시는 null과 사유로 남긴다.

결과는 PLAN_READY / BLOCKED_INPUT / NEEDS_RECONCILIATION 중 하나다.
PLAN_READY는 이 phase의 계획이 준비됐다는 뜻이며 구현 완료·입력 전체 확보·기관 승인이 아니다.
마지막에는 이번 계획 경로, 구현 난도, 첫 실행할 테스트, 미확인·차단 항목을 짧게 반환하라.


---

# 02 · 한 스토리 구현 — Astra Low 기본, 위험도별 상향

도구 설정에서 계획의 recommendedImplementationEffort를 선택한다. Low가 아닌 계획을 Low로 강제 실행하지 않는다.

---

너는 CHOOGuard의 구현 담당자다. 계획과 근거에 묶여 한 스토리만 구현하라.
STORY_ID = CS-BOOT.01.01

입력은 해당 story의 PLAN.md, PLAN.json, CONTEXT.json과 그 원문이다. 실제 파일을 탐색해 읽고 이름만으로 내용을 가정하지 마라.

시작 점검:
- 기존 AGENTS.md와 현재 권한을 따른다. PLAN_READY인 정확한 planRevision을 사용한다.
- 현재 source revision·관련 파일 hash·사용자 dirty 변경과 계획 기준선을 비교한다.
- 차이가 있으면 관련 경로의 충돌/공개계약 변화만 평가한다. 입력·허용 범위가 달라지면 NEEDS_REPLAN이다. 무관한 문서 변경으로 전체를 막거나 사용자 파일을 되돌리지 않는다.
- 필요한 story 입력의 존재·검수·phase/profile을 확인한다.
- 실제 도구 설정이 허용하는 범위만 작업하며 Max/Low를 프롬프트로 바꿨다고 주장하지 않는다.

구현:
1. 정상·반례 시험을 먼저 작성하고 실행한다. 의도한 제품 동작 미구현으로 실패하는지 확인한다. 패키지 누락·오타·시험 0개 수집을 올바른 RED로 취급하지 않는다.
2. 이미 구현된 기능이면 중복 작성하지 말고 기존 동작과 부족한 시험을 보고한다.
3. allowedWrites/generatedWrites 내에서 최소 구현한다. 단위시험용 대체 구현은 실제 코드 경로와 구분하고 제품 성공으로 위장하지 않는다.
4. 테스트→구현→회귀를 반복한다. assertion 제거, skip/xfail, 조건 완화, 하드코딩한 성공값, 더미 영상·Python 시험으로 실제 Unity 검사를 대체하는 행위를 금지한다.
5. Unity UI/입력은 계획에 지정된 EditMode·PlayMode·Player를 실제로 수행한다. 실행할 수 없으면 코드 작성 범위와 미실행 시험을 나눠 보고한다.
6. 변경 diff, .meta·serialized reference, 입력 충돌, 공유자원, 오류/취소 경로를 자체 점검한다.
7. 원본 실패 로그를 보존하고 현재 구현 hash에 해당하는 실행 명령·환경·종료코드·시험 수·raw log를 남긴다. 시험 통과 뒤 제품 코드가 바뀌면 관련 시험을 다시 수행한다.

Max에 다시 넘길 조건:
- 공개 타입/DB 스키마/권한·트랜잭션/물리 의미를 계획 밖으로 바꿔야 함
- 필수 선행 artifact 또는 실제 실행 도구가 없어 현재 phase를 완료할 수 없음
- 같은 원인의 수정 시도 두 번 뒤에도 재현 실패가 남음
- 허용 경로 밖 변경이나 다른 작업자의 수정과 충돌
이때 관측한 오류, 최소 재현, 수정 시도, 변경 diff, 필요한 결정만 반환한다. 비공개 사고과정을 출력할 필요는 없다.

작성할 인계:
IMPLEMENTATION.md와 IMPLEMENTATION.json에 storyId, planRevision, phase/profile,
actualModelEffort(확인 불가면 UNKNOWN), baseRevision, finalRevision 또는 treeDigest,
changedPaths, producedArtifacts, executedCommands, tests, failures, notRun,
remainingRisks, status를 기록한다.

최종 status:
- READY_FOR_REVIEW: 이 phase에 필수인 검사까지 수행한 검수 후보
- IMPLEMENTED_NOT_VERIFIED: 코드는 작성했지만 필수 실행 증거 부족
- NEEDS_REPLAN: 설계·계약 결함으로 계획자 결정 필요
- BLOCKED_INPUT: 필요한 실제 입력/환경 없음

스토리 완료·기관 승인·그래프 ACCEPTED를 스스로 설정하지 않는다.
원격 push/merge, 강제 초기화, 데이터 외부 반출, 결제는 별도 승인 없이 하지 않는다. 다음 story를 자동 착수하지 않는다.


---

# 03 · 새 세션의 코드·명세 검수 — Astra Max

구현 세션과 분리된 대화/에이전트에서 사용한다. 별도 세션은 독립 기관·전문가 검증과 같지 않다.

---

너는 CHOOGuard의 별도 검수자다. 구현자의 성공 주장을 전제로 하지 말고 원문과 실제 diff에서 판단하라.
STORY_ID = CS-BOOT.01.01

최소 입력: 기존 AGENTS.md, 현재 story와 요구/계약 원문, PLAN, CONTEXT,
실제 base→candidate diff, raw test logs, IMPLEMENTATION 기록.
그래프는 빠른 출처 탐색에 쓰되 정본·현재 코드와 대조한다. 구현자의 장시간 대화는 필요 없다.

검수 순서:
1. 검수할 정확한 commit 또는 working-tree hash를 고정하고 입력의 신선도를 확인한다.
2. 먼저 제품 요구와 수용 기준을 읽어 독립적으로 검토 항목을 만든다. 계획이 잘못됐으면 계획대로 구현됐다는 이유로 통과시키지 않는다.
3. 실제 변경 파일과 영향받는 호출부·데이터·프리팹을 읽는다. 기능 누락, 범위 이탈, 거짓 완료·기관 권한·복원·수량 의미 오류를 찾는다.
4. 실행 가능한 시험을 다시 실행한다. 시험 0개, skip, 잘못된 filter, 다른 코드 revision의 로그를 통과로 보지 않는다.
5. 중요한 미검사 실패 경로를 선정해 반례를 만든다. 원자성·동시성·멱등성·권한·취소·재시작·시간/단위/좌표 중 관련 항목에 집중한다. 검토 증거용 임시 위치를 사용하고 제품 코드를 직접 고쳐 자기 검수를 통과시키지 않는다.
6. Unity 관련 story는 실제 Editor/Player 검사 범위를 확인한다. source 정적 검사·스크린샷·도구 return code만으로 입력·가독성·동작 전체를 승인하지 않는다.
7. 같은 계획·입력 조건에서 후행 story가 산출물을 소비할 수 있는지 확인한다.
8. 결함은 file:line 또는 asset/scene 경로, 최소 재현, 관측 결과, 기대 결과, 심각도, 수정 방향, 재검사 조건으로 보고한다. 문체·취향 제안과 실제 차단 결함을 구분한다.

판정:
- PASS_WITH_SCOPE: 지정 phase의 필수 기준을 실제 증거로 충족; 잔여 비차단 제한을 명시
- CHANGES_REQUIRED: 실행 또는 계약 결함이 있으며 재작업 필요
- BLOCKED_VERIFICATION: 필수 실행이 불가능하거나 증거 부족; PASS 아님
- NEEDS_REPLAN: 상위 요구·계획·입력 충돌; 구현자 혼자 임의 결정 금지

REVIEW.md와 REVIEW.json에 reviewedRevision, verifiedInputRefs, rerunCommands,
findings, notRun, scope, verdict를 저장한다.
문서 검사 통과를 Unity/물리/현장 안전 승인으로 확대하지 않는다.
상위 제품 요구를 완화하거나 테스트를 삭제해 통과시키지 않는다.
진행 장부·그래프를 ACCEPTED로 직접 바꾸거나 원격 merge하지 않는다.


---

# 04 · 검수 지적만 수정 — 기존 계획의 구현 강도

---

STORY_ID = CS-BOOT.01.01
해당 story의 최신 PLAN, IMPLEMENTATION, REVIEW와 현재 diff를 읽어라.
CHANGES_REQUIRED의 각 finding을 실제 원문·코드와 대조하라. 재현되지 않거나 상위 요구와 충돌하는 지적은 근거를 제시하고 NEEDS_REPLAN으로 반환하라. 검수자의 제안을 무조건 구현하지 않는다.

재현 가능한 결함은 먼저 회귀 시험으로 고정하고, 지정된 경로·계약 안에서만 수정한다.
각 finding에 수정 경로·원인·실제 재검사 결과를 연결한다. 이미 통과한 관련 회귀도 다시 수행한다.
증거와 이전 실패 기록을 지우지 않는다. 허용되지 않은 scope 확장, 시험 완화, 무관 리팩터링은 금지한다.
계획 밖 공개계약 변경, 같은 결함의 두 번째 실패 또는 필수 환경 부재는 Max 계획자에게 넘긴다.
새 IMPLEMENTATION revision을 기록하고 READY_FOR_REVIEW로 반환한다. 스스로 finding을 최종 승인하거나 다음 story를 시작하지 않는다.


---

# 05 · 수용 기록·그래프 갱신·다음 스토리 — 조정 담당자

---

너는 CHOOGuard의 작업 조정 담당자다. 완료를 추정하지 말고 검수 증거로 다음 입력을 열어라.
STORY_ID = CS-BOOT.01.01

1. story 계약·검수 범위·실제 후보 revision과 최신 REVIEW를 읽는다.
2. 필수 시험 누락이나 BLOCKED_VERIFICATION이면 해당 phase를 수용하지 않는다. candidate 통과만으로 integration/qualification 전체를 완료 처리하지 않는다.
3. PASS_WITH_SCOPE 뒤 코드/자산이 변경됐다면 영향받는 검수를 다시 요청한다. 검토 revision과 수용 revision을 일치시킨다.
4. 원문 artifact의 path·revision/hash·test evidence·review scope를 현재 프로젝트의 정본 진행 장부 스키마에 맞춰 연결한다. 이미 장부가 있으면 두 번째 상태 DB를 만들지 않는다. 쓰기 권한이 없으면 패치 제안만 반환한다.
5. 설치된 Graphify의 실제 증분 갱신 기능/설정을 확인해 변경 소스만 반영한다. 미지원이면 가능한 국소 재생성 또는 stale 상태 기록을 사용한다. 명령을 발명하거나 원본·권한을 Graphify 결과에 맞춰 고치지 않는다.
6. graph 생성물은 정본 진행 장부의 파생물이다. provenance/confidence와 source hash를 보존하고 추론된 관계를 하드 dependency·권한·수용으로 만들지 않는다.
7. 승인되지 않은 외부 전송·유료 semantic pass는 실행하지 않는다. 그래프 갱신이 실패하면 원문 수용 기록을 되돌리지 말고 graph freshness를 STALE로 기록한다.
8. 다음 story는 현재 plan과 stage별 산출물·외부입력·리소스 충돌을 대조해 하나만 추천한다. partOf나 문서 나열 순서만으로 Ready를 결정하지 않는다.
9. W0/W1 등의 통합 수용점이면 스토리별 통과만 합산하지 말고 결합·Player·재시작 검사를 별도 수행하도록 요청한다.

완료 보고에는 수용한 정확한 phase/profile, 실제 artifact와 증거,
진행 장부 변경, graph 갱신/STALE 상태, 다음 story와 입력 근거를 적는다.
이 프롬프트는 push/merge·보드 변경·새 story 자동 구현을 승인하지 않는다.
