# 02 · 통합 제품 구현 — 기본 반복

STORY_ID = CS-EXEC.01.01

Astra low 워커가 구현하고 메인 모델이 오케스트레이션과 최종 결정을 맡는다. 독립적인 쓰기 범위는 병렬 실행한다. 실제 모델/effort 확인 불가면 UNKNOWN이며 프롬프트로 설정을 바꿨다고 주장하지 않는다.
`docs/CHOOGuard_Story_Plan_v5/ACTIVE_STORY.md`와 같은 폴더의 `plan.json`, `state/progress.json`에서 현재 범위·진척·보류를 확인한다. 이번 범위와 관계있는 v4 원문·코드·시험만 읽는다.

1. 이번 변경 범위와 허용 writes를 확인한다. v5는 개발 절차만 대체하고 v4/basis 요구·AC/AT·산출물·제품/안전 계약은 유지한다. 기술 의존성과 실제 입력은 필요하지만 legacy 4단계 receipt·parent gate·작업창 승인은 요구하지 않는다.
2. 기존 구현을 재사용해 바로 동작하는 MVP를 구현한다. TDD·RED 단계는 요구하지 않는다. 한 Story 아래에서 화면과 실제 처리 흐름의 연결을 우선하고 세부 기능을 새 Story로 분해하지 않는다.
3. 컴파일과 변경된 핵심 사용자 흐름만 확인한다. 구체적인 실패가 없으면 전체 제품 시험·반복 검수를 확대하지 않는다. Unity 제품 동작을 Python 검사로 대체하거나 assertion 완화·skip·가짜 성공·증거 덮어쓰기를 하지 않는다.
4. 실패와 미실행은 관련 범위·이유를 남긴다. Windows/현장 입력이 없으면 해당 검사만 보류하고 독립적으로 가능한 구현을 진행한다. 필수 Windows x64/Mono 개발 빌드·실행 기준을 삭제하지 않으며 선택 검사를 새 필수 승인 게이트로 올리지 않는다.
5. v5 `state/progress.json` 한 곳에 기능별 완료/부분/대기/미착수, 실제 명령·결과 경로·실패/보류를 갱신한다. `python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py render`로 뷰를 갱신한다. 시험 개수는 개발 진척률이 아니다.
6. 워커는 변경 경로·실제 결과·한계를 작은 JSON graph로 보고한다. 메인은 필요 노드만 읽고 직접 증거를 확인한다. Jev는 발주 중복·실패 원인·약속 후 중단·보고 과장을 짧게 판정하고, 승인된 내부 운영 산술의 입력으로 사용한다. 공개 예측/그래프 패널을 추가하지 않는다. 메인이 재배정·재개·수용을 결정하고 A 품질은 QUALITY_A_RUBRIC.md의 실제 증거로 판정한다. 작은 항목마다 멈춰 다음 Story를 선택하지 않는다.

설계 변경·안전/데이터 무결성 문제·같은 원인 두 번 실패·허용 범위 충돌이면 해당 문제만 좁혀 판단을 요청한다. 통합은 무제한 쓰기나 자동 수용 권한이 아니다. Bootstrap의 IMPLEMENTED_NOT_VERIFIED는 해당 부분에만 적용한다.

Story별 PLAN/CONTEXT/IMPLEMENTATION/REVIEW 세트, 반복 전체 hash/graph 검사, 불변 환경 재설치/재탐색은 만들지 않는다. 기존 v4 장부·Bootstrap 원시 증거를 보존한다. 별도 승인 없는 worktree·commit·push·PR·설치·외부 게시·graph 재생성을 하지 않는다.
