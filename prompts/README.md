# CHOOGuard · 단일 Story 실행

활성 Story는 **CS-EXEC.01.01 — CHOOGuard 통합 제품 구현** 하나다.
이번 MVP의 확정 방향은 [MVP_DIRECTION.md](../docs/CHOOGuard_Story_Plan_v5/MVP_DIRECTION.md)에 따른다: 부산역~남포동~북항 기반 한국어 비상대응 운영 게임이다. 실제 공간과 인물의 크기 관계 및 출처에 근거한 복원을 요구하며, 미확인 수치는 추정으로 구분한다.
사람이 볼 계획·진척은 [ACTIVE_STORY.md](../docs/CHOOGuard_Story_Plan_v5/ACTIVE_STORY.md), 기록은 v5 `plan.json`과 `state/progress.json`이다.
전체 제품은 IN_PROGRESS / NOT_ACCEPTED다. 현행 구현과 좁은 native 증거는 v5 진행 기록 및 QUALITY_A_RUBRIC.md 기준으로 확인한다. Windows·Player/수동 수용 등 보류를 유지한다.

## 기본 반복

**`02-implement-story-low.md` 하나**로 `이번 MVP 범위 확인 → Astra low 병렬 구현 → 컴파일·핵심 흐름 확인 → v5 진행 기록 한 곳 갱신`을 반복한다.
메인 모델이 작업 배정·통합·수용 결정을 맡고, 쓰기 경로가 겹치지 않는 Astra low 워커가 구현한다. TDD나 작은 항목별 승인·인계 세트·다음 Story 선택은 하지 않는다. 실제 모델/effort를 확인하지 못하면 UNKNOWN으로 보고한다. 프롬프트가 런타임 설정을 바꾸지는 않는다.
Jev는 작은 상태 묶음으로 중복·중단·실패 원인·보고 과장을 판정하는 보조 수단이다. 코드 작성·승인 권한은 없고, 실제 도구 출력과 메인 모델의 결정이 우선한다. 워커는 경로·변경·검증·한계를 담은 작은 graph로 보고한다.

나머지 파일은 순차 필수 단계가 아니라 필요할 때만 쓰는 선택 도구다. 파일명은 호환을 위해 유지한다.
- `00-context-audit-max.md`: 필요한 입력 조사
- `01-plan-story-max.md`: 큰 설계 변경의 좁은 결정
- `03-review-story-max.md`: 필요한 변경 코드 검수
- `04-repair-story.md`: 재현된 결함 수정
- `05-accept-refresh-next.md`: 최종 정리·수용 범위 기록. 다음 Story 추천·graph 갱신 단계가 아니다.

## 원문과 안전 경계

v5는 **개발 절차만 대체**한다. [v4 정본](../docs/CHOOGuard_Story_Plan_v4/plan.json)의 109개 ID는 요구·AC/AT·산출물·writes·기술 의존성·basis 제품/안전 계약의 원문 참조이며 활성 children이나 실행 게이트가 아니다. 기술 순서와 실제 입력은 확인하되 과거 4단계 receipt·parent gate·작업창 승인을 재요구하지 않는다.

Unity native PC/uGUI/TMP/Input System, 두 제품 모드, Windows x64/Mono 개발 빌드·실행 등 필수 제품 수용 기준은 유지한다. 통합은 무제한 쓰기·기관 승인 권한이 아니다. 선택적 검사를 새 필수 게이트로 승격하지 않는다.

복수 모델 전수검수·자동 검수 루프·반복 전체 hash/graph 검사·불변 환경 재설치·문서 보완용 Unity 실행은 기본 절차가 아니다. 안전/데이터 무결성·설계 변경·동일 원인 두 번 실패·범위 충돌만 좁혀 판단을 요청한다. 실제 실패/미실행을 숨기지 않으며 관련 환경 부재로 독립 구현까지 막지 않는다.

기존 Bootstrap 인계·원시 증거·v4 진행 장부는 과거 기록으로 보존한다. 기존 합본 `CHOOGuard_Astra_Graphify_Prompts.md`는 과거 자료이며 활성 실행 지침으로 사용하지 않는다. 명시 승인 없는 worktree·commit·push·PR·외부 게시·설치·graph 재생성은 하지 않는다.
