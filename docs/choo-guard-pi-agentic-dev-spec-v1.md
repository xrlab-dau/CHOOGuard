# choo guard Pi 에이전틱 개발 명세 v1.1

- 상위 문서: 파이프라인 v1.2 §5~9, 백로그 v1.3, 발견 v1, ADR 0001~0005.
- 범위: spec-driven 및 subagent-driven 개발의 세션·그래프·증거 계약.
- 등급: PUBLIC_SYNTHETIC.
- **상태: changes_required, 운영 실행 보류.** 아래 요구는 현재 설정이 보장하는 기능이 아니다. 필수 관점이 실패한 최초 검토는 cannot_proceed이며 수정 후 독립 재검토가 필요하다.

## 1. 역할과 실제 실행 경계

| 역할 | 프로파일 | 선언 모델 | 선언 도구 | 요구 경계 |
|---|---|---|---|---|
| 컨트롤러 | PM과 주 세션 | 허용목록으로 고정 필요 | 승인된 범위만 | manifest·실제 제공자·판정 검증과 저장 |
| 정찰 | scout | anthropic/claude-sonnet-5 | read/grep/find/ls | 승인 입력 읽기 전용 |
| 구현 | implementer | anthropic/claude-sonnet-5 | read/bash/edit/write/grep/find/ls | 보호 경로 제외한 격리 실행 사본 |
| 문서 수정 | doc-reviser | anthropic/claude-sonnet-5 | read/edit/write/grep/find/ls | 명시된 비보호 문서만 |
| 명세 검토 | spec-reviewer | openai-codex/gpt-5.6-sol | read/grep/find/ls | 불변 대상, 모델 밖 읽기 전용 |
| 적대적 검토 | adversarial-reviewer | openai-codex/gpt-5.6-sol | read/grep/find/ls | 불변 대상, 모델 밖 읽기 전용 |
| 안전 검토 | safety-auditor | openai-codex/gpt-5.6-sol | read/grep/find/ls | 불변 대상, 모델 밖 읽기 전용 |
| 실행 검증 | verifier | openai-codex/gpt-5.6-sol | read/bash/grep/find/ls | 통합 소스는 읽기 전용, 승인 생성·빌드·임시 로그만 쓰기 |

Pi 프로파일은 의도된 제공자 분리를 표현한다. pi-agents RPC 호출에서 modelScope·watchdog·역할 매핑·override·상속·fallback이 실제 적용되는지는 M1-07 시험 전 미확인이다. 서로 다른 프로세스라는 사실만으로 파일·자격·네트워크 격리가 생기지 않는다. 조사 하네스와 reducer도 별도 실제 모델 검증 대상이다.

## 2. 발견부터 병합까지

| 단계 | 산출물 | 게이트 |
|---|---|---|
| BMAD 방법 기반 발견 | product brief, 요구사항 발견, A/D/AIN 추적 | 기준선과 PM 입력 구분, 미정 유지 |
| Spec Kit 변경 단위 명세 | spec.md, 품질 checklist | 명확성·완전성·일관성 검토. 파일 생성과 CLI 실행을 구분 |
| 명세·설계 독립 검토 | docs/reviews의 정제 판정 | 모든 필수 관점·실제 제공자·불변 대상·저장 확인 |
| plan·tasks | 승인 명세의 구현·시험 계획 | 미해결 P1~P3 또는 cannot_proceed로 다음 단계 금지 |
| 구현과 검증 | 최소 변경, RED/GREEN 결과, 고정 test ID | 보호 경로 별도 승인, 리뷰 approved와 검증 성공 모두 필요 |
| 인계와 병합 | 검토된 커밋·증거·사람 PR | develop squash, 팀·CODEOWNER 승인, 필수 검사 |

로컬 Spec Kit `create-new-feature.sh`는 명세 디렉터리와 feature 상태를 만들지만 Git 브랜치를 생성하지 않는다. `BRANCH_NAME` 출력만으로 브랜치 생성으로 해석하지 않는다. 현재 명세는 직접 작성했고 `SPECIFY_FEATURE_DIRECTORY`로 지정한다. Git 브랜치는 별도로 Git Flow를 따른다.

## 3. 그래프 계약과 현 구현 차이

현재 wu-develop 순서는 scout, implementer, adversarial-review, 수정·재검토 루프, verifier다. 현재 YAML이 미승인 결과를 확실히 차단한 뒤 verifier로 진행하는지는 충족되지 않았다.

1. 실행 대상은 base/head와 미커밋 입력까지 포함한 상대 경로·해시 manifest로 고정한다. 리뷰 시작·종료·적용 직전 일치해야 한다.
2. 리뷰어 출력은 `outcome, actionable, notes`다. 지적은 `id, severity, title, problem, fix, locations`를 가진다. outcome은 approved/changes_required/cannot_proceed, severity는 P1/P2/P3다.
3. reducer 출력은 `outcome, reason, actionable, report`다. Pi의 SEC-와 대체 워크플로의 SAF-는 출처를 보존한다. 지적 키는 bundle/lens/id이며 접두어만으로 병합하지 않는다.
4. `onError: collect`는 오류 수집 방식이지 승인 정책이 아니다. 필수 관점 오류·누락·스키마 실패·제공자 불일치·대상 변경은 cannot_proceed다. 현재 나머지 결과로 승인하는 reducer는 이 계약과 충돌한다.
5. 현재 초기 리뷰 뒤 `max: 3` 수정 루프는 최대 4회 리뷰다. 목표인 총 3라운드와 다르다. PM 승인 후 YAML과 시험을 함께 수정하기 전 운영하지 않는다. 상한 도달도 자동 승인이나 재검토 면제가 아니다.
6. 사람 수정·반박·대상 변경 후에도 새 불변 대상으로 독립 재검토한다. 컨트롤러가 정제 판정을 append-only로 저장하고 실패하면 다음 라운드를 막는다. 현 YAML에는 이 저장을 증명하는 경로가 없다.
7. verifier는 승인된 test ID와 검토된 고정 argv만 받는다. 자유 `tests` 문자열을 셸에 넘기는 현 구현은 사용하지 않는다. 보호 경로 변경 여부도 에이전트 자기 보고 대신 실제 diff와 경계로 판정한다.

Pi YAML 파싱 성공은 위 요구·분리 세션·학교 PC 재현·차단 성공의 증거가 아니다. 이번 검토는 Claude Code Workflow에서 수행했으며 Pi 런타임 검증으로 바꾸어 기록하지 않는다.

## 4. 머신·전송·승인

| 작업 | local | school-pc |
|---|---|---|
| 발견·명세·설계·공개 기술 조사 | 승인 프로파일 후보 | 조사·모델·Exa 기본 비허용 |
| 사람 정적 부트스트랩 검사 | 수행 필요 | 환경 확인 후 수행 필요 |
| Unity·MCP·HMD | 금지 | 선행 승인과 시험 후만 |
| 실제 촬영·재구성·회신 | 모델 입력 금지 | D-07 및 KORAIL 조건 확정 전 비허용 |

기본 네트워크 거부, PUBLIC_SYNTHETIC만 외부 전송한다. 자료 접근 승인, 모델 전송 승인, 공개·병합 승인은 서로 대체하지 않는다. 보호 범위는 최소 AGENTS.md, .pi, .specify/memory, scripts/ci, .github, docs/evidence이며 확장은 PM이 결정한다. 현재 설정 간 범위 차이는 해결 전 보류다.

현재 `/research` 프롬프트의 문자열 인자 전달과 하네스의 자유 모델·출력·금칙어 통제는 불완전하다. 직접 curl로 우회하지 않는다. 패키지 설치·trust·로그인·워크플로 실행 명령은 공급망과 M1 실행 경계 시험 전 실행 안내로 제공하지 않는다.

## 5. 완료 판정

[워크플로 상태](reviews/2026-09-06-workflow-status.json)와 [지적 처리 기록](reviews/2026-09-06-review-dispositions.md)을 확인한다. docs/evidence의 확정 영수증은 사람 승인 후 새 파일로 추가한다. 에이전트가 서명·공개승인을 만들지 않는다. docs/reviews에는 원문 대신 정제 판정과 실제 관측 한계를 남긴다.

현재 문서 정정은 운영 통제 구현·권한 승인·독립 재검토를 대신하지 않는다. 이 브랜치는 실행 준비 완료가 아닌 검토용 초안이다.

## 변경 이력

| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-09-06 | 최초 작성 |
| 1.1 | 2026-09-06 | 선언과 강제 분리, 실패 차단·스키마·라운드·불변 대상·test ID 계약 정정, 조기 실행 안내 제거 |
