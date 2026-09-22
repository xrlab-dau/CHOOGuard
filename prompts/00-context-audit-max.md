# 00 · 필요한 입력 조사 — 선택 도구

STORY_ID = CS-EXEC.01.01

필요한 입력이 불명확할 때만 사용한다. `docs/CHOOGuard_Story_Plan_v5/ACTIVE_STORY.md`와 같은 폴더의 `plan.json`, `state/progress.json`을 읽고 이번 변경에 필요한 원문·기존 코드·입력만 조사하라.

- v5는 개발 절차만 대체한다. v4/basis 제품·안전 계약과 원문 writes·AC/AT는 유지한다. 109개 legacy ID를 새 작업 목록이나 receipt 게이트로 되살리지 않는다.
- Bootstrap 부분 로컬 구현과 보류된 Windows/Player 수용을 구분한다. 전체 제품 완료로 추정하지 않는다.
- 기존 사용자 변경과 쓰기 권한을 지킨다. 이미 확인된 불변 환경의 재탐색·재설치·Unity 실행을 반복하지 않는다.
- 실제 입력 부재는 해당 구현/검증 범위만 표시한다. 독립 구현을 불필요하게 막지 않는다. graph는 선택적 출처 인덱스이며 승인 근거가 아니다.
- 필요한 경로, 부족한 입력, 영향, 결정할 사항만 짧게 반환한다. 새 CONTEXT_INDEX·Story별 인계 세트·전체 계획은 만들지 않는다.

제품 수정·자동 agent/검수 루프·전체 hash/graph 검사·외부 전송·설치·commit/push/PR을 이 프롬프트로 승인하지 않는다.
