# 04 · 재현된 결함 수정 — 선택 도구

STORY_ID = CS-EXEC.01.01

`docs/CHOOGuard_Story_Plan_v5/ACTIVE_STORY.md`, v5 JSON, 지적된 변경분과 관련 v4 원문을 읽어라. v5는 개발 절차만 대체하고 v4/basis 제품·안전 계약은 유지한다.

1. 지적을 실제 코드·요구와 대조한다. 재현되지 않거나 상위 계약과 충돌하면 근거와 필요한 결정만 반환한다.
2. 재현되는 결함을 작은 회귀로 먼저 실패시키고 승인된 경로 안에서 최소 수정한다. 관련 시험만 재실행한다. 시험 완화·무관 리팩터링·원시 실패 기록 덮어쓰기는 금지한다.
3. 같은 원인 두 번 실패, 설계/안전/데이터 무결성 변경, 범위 충돌이면 해당 문제만 판단을 요청한다. 환경 부재는 해당 검증만 보류한다.
4. v5 `state/progress.json`에 수정 범위·결과 경로·실패/미실행을 기록하고 `python3 -B docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py render`로 뷰를 갱신한다.

Story별 인계 revision·전체 계획·자동 review loop·다음 Story 선택을 만들지 않는다. Bootstrap 부분 성공을 전체 완료로 승격하지 않는다. 기존 증거·v4 장부를 보존하며 전체 Unity/hash/graph 검사·설치·commit/push/PR·외부 게시를 요구하지 않는다.
