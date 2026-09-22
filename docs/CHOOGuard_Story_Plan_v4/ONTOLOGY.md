# 실행 계획 온톨로지

## 목적
LLM이 ‘무엇을 만들까’뿐 아니라 ‘지금 어느 입력으로 어느 범위까지 실행·검수할 수 있는가’를 읽게 한다. 이것은 보편적인 단일 LLM 온톨로지 표준이 아니라 CHOOGuard의 도메인 모델이다.

## 개념
| 개념 | 의미 | 혼동하면 안 되는 것 |
|---|---|---|
| DeliveryPlan | 신규 제품의 전달 계획 | 실제 실행·권한 부여 |
| Epic / CapabilityTask | 11개 기능영역 / 48개 상위 구현 단위 | 전체 종료가 모든 자식의 선행 조건 |
| Story | 관찰 가능한 결과 하나와 반례·인계 | 파일 몇 개가 있다는 사실만으로 완료 |
| ArtifactSpec | 만들 파일·기능·계약의 선언 | 이미 존재하는 ArtifactRevision |
| ArtifactRevision | 실제 존재하는 경로·해시·코드 revision의 결과 | 모델의 진실성·기관 승인 |
| Dependency | 생산 story·artifact·단계→소비 story·단계·condition | 단순 관련성/containment |
| AcceptanceCriterion | Given–When–Then으로 정의한 시험 | 시험을 실행한 EvidenceRecord |
| ExecutionAttempt | 실제 명령·입력·환경·시각을 가진 실행 | 계획 단계나 예상 소요시간 |
| EvidenceRecord | 원시 결과·환경·해시와 검증 범위 | 파일 존재만으로 독립 승인 |
| ReviewDecision | 정확 입력과 revision에 대한 검수 결정 | 작성자가 임의로 기록한 보편 AAA |
| WorkWindow | 한 수용 목표에 묶인 단기 스토리 | 가용시간이 없는 고정 1주/2주 약속 |
| ExternalInput | 설치 환경·원본·동의·독립 기준·고객 양식 | 없으면 모든 개발이 중지된다는 조건 |
| ResourceClaim | 실행 시점의 경로·Editor·출력 리소스 사용 선언 | 문서 속 이름만으로 얻은 분산 잠금 |

## 관계의 정확한 의미
`partOf`는 구조만 나타낸다. 순서나 권한을 만들지 않는다.
`fulfills`는 부모가 소유하는 요구의 구현 범위를 나타낸다. `supports`는 다른 소유자가 있는 요구를 지원한다. 둘은 실행 검증 결과가 아니다.
`producesSpec`는 예정 산출물 선언이다. 실행이 실제 파일을 만들었을 때만 별도 ArtifactRevision과 EvidenceRecord를 생성한다.
`dependsOn`은 Dependency 노드로 연결한다. 그 노드에는 `producer`, `artifact`, `producerStage`, `consumer`, `consumerStage`, `condition`을 모두 넣는다.
`writeTarget`은 수정 경합 판단에 쓰며 실제 경로 권한이 아니다.
`ordinal`은 동시 가능한 작업의 추천 우선순위다. dependency보다 우선하지 않는다.

## 예시
`CS-OPS.02.02.candidate`는 ‘이벤트·예약·receipt를 함께 저장하는 후보 코드’를 생성한다. 이후 UI 통합이 필요한 것은 이 특정 산출물의 통합 증거이지 **CS-OPS 전체 에픽 종료**가 아니다.
`CS-PLAY.02.02.candidate`는 고정 port와 명시적 double로 구현할 수 있다. `integration`에서는 실제 run/store receipt를 요구한다. double 통과를 실제 저장 통과로 간주하지 않는다.
기하 holdout은 현장 정량 qualification을 제한한다. 두 공간 fixture의 UI 입력 제작을 전역 차단하지 않는다.

## 상태와 open/closed world
정본 계획의 story는 모두 NOT_STARTED이고 시험은 NOT_RUN이다. 실제 결과는 별도 state 파일에만 기록한다. `missing`, `not-run`, `failed`, `rejected`, `unsupported`는 같지 않다.
등록되지 않은 prerequisite·condition·profile은 조용히 false로 처리하지 않고 오류다. 자료가 없다는 사실로 현장의 상태가 거짓이라는 결론을 내리지 않는다. 실행 준비도 계산만 선언된 입력의 부재를 HOLD로 취급한다.
반대로 ‘문서 읽기 가능’은 ‘구현 실행 가능’도, ‘실행 권한 있음’도 아니다.

## 기술 계층
JSON Schema 2020-12: 스토리·진행기록의 타입/필수필드/허용값.
응용 검사기: ID 참조, 단계별 DAG, file/.meta 경합, 단기 범위의 선행조건 닫힘, 증거 바인딩, profile 일치.
JSON-LD 1.1 + RDF: 개념·관계의 교환/검색. `@context`는 로컬 내장이라 원격 컨텍스트 다운로드가 필요 없다.
PROV-O: Story는 계획, ExecutionAttempt는 실제 활동, ArtifactRevision/EvidenceRecord는 실제 개체에 해당한다. 아직 실행이 없어 그래프에 실제 execution 활동을 만들지 않았다.
SHACL: 관계 검증 shape를 제공한다. 이번 환경에는 엔진이 없어 실행 검증하지 않았으며 파싱·응용 검사를 SHACL 엔진 PASS로 부르지 않는다.

## 왜 최신 방법과 오래된 표준을 함께 쓰나
2026-02-11 OpenAI의 harness engineering에서 작은 진입 문서·저장소 지식·기계적 피드백을, Anthropic의 2025-09-29 context engineering에서 최소 충분 문맥과 점진적 로드를 참고했다. 브라우저 테스트나 무인 merge 정책은 Unity 계획에 복사하지 않았다.
JSON-LD/PROV/SHACL/JSON Schema는 성숙한 기반 표준이다. 최근 발명이라고 표기하지 않는다. 이 구성의 모델별 생산성 우위는 아직 측정하지 않았다.
원문과 사용 범위는 `contracts/method-sources.json` 및 `SOURCES.md`에 있다.
