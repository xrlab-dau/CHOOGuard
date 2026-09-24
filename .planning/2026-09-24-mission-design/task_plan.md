# Task Plan: 본편 미션 설계 정본(G1)과 G2 벤치마크 대조표

## Goal
B → A 순서. ① 본편(비상대응) 미션의 설계 정본을 만들어 G1이 평가할 대상을 확정한다. ② 그 위에서 G2가 요구하는 source-to-runtime 대조표를 FPS 기준으로 다시 쓴다.

## Next Step
Phase 1~3 완료. 다음은 승객 NPC·유도 구현 — `MISSION_DESIGN.md` 8절, `BENCHMARK_FPS_MATRIX.md` 7절(축 3 이 통째로 미구현)이 같은 결론이다. 상세는 `progress.md`.

## Current Phase
완료

## 착수 시점의 사실 (2026-09-24 실측)

- **본편 행동 코드가 없다.** `App/Fps/` 전체에서 신고·무전·대피·통제·승객을 구현한 파일 0개. 걸리는 것은 전부 튜토리얼 계층의 절차 텍스트·감사 문구다.
- **NPC 0, AudioSource 0.**
- **미션 설계 정본이 없다.** `Story_Plan_v5`에 `TUTORIAL_MAINTENANCE_DESIGN.md`는 있으나 본편 대응물이 없다.
- **재료는 이미 있다.** `FPS_RESEARCH_PROPOSAL.md`에 실행 그래프(mermaid), 플레이 선택별 결과·잘못된 학습 방지표, 첫 미션 후보와 후속 후보, Jev 054·055 판정이 들어 있다.

튜토리얼 때와 같은 패턴이다 — 제안은 있는데 채택 기록이 없어 평가를 시작할 수 없다. A-FPS-1 루브릭도 `A-FPS-1-PROPOSED` 상태로 방치돼 있었다.

## 규율 (어기지 않는다)

- **새로 창작하지 않는다.** 기존 제안·판정을 채택하고, 채택하지 않은 것은 이유를 적는다.
- **근거 없는 절차를 공식처럼 제시하지 않는다.** 내부 매뉴얼 미확보 상태다(`manual_fidelity` 3.81/5).
- **완료는 월드 상태로 판정한다.** 버튼 순서나 타이머 만료를 완료로 쓰지 않는다 — 튜토리얼 계층이 이미 그 규율로 서 있다.
- **역할 경계를 넘기지 않는다.** 역무 직원이 전문 대응자의 업무를 대신하지 않는다.
- **구현 상태를 과장하지 않는다.** 설계 문서가 생겼다고 G1이 올라가지 않는다.

## Phases

### Phase 1: 미션 설계 정본 (B)
- [x] `MISSION_DESIGN.md` — 채택 기록 형식. 첫 미션·실행 그래프·완료 판정 규율·튜토리얼 연결
- [x] G1이 요구하는 3종 세션(재난 1·테러 1·변형)과 후보의 대응 관계를 표로
- [x] 현재 구현 상태를 실측으로 기재(본편 코드 0, NPC 0, 오디오 0)
- [x] `QUALITY_A_RUBRIC.md` G1·`MVP_DIRECTION.md`에서 링크
- **Status:** complete

### Phase 2: G2 벤치마크 대조표 (A)
- [x] `PRODUCTION_GAME_BENCHMARK.md`가 RTS 전제(기관 지시·시민 집단)인 부분을 식별
- [x] `FPS_RESEARCH_PROPOSAL.md`의 1인칭 레퍼런스와 합쳐 source-to-runtime 대조표 작성
- [x] 단일 1인칭 행위자에 맞지 않는 항목은 '가져오지 않음'으로 명시
- [x] 런타임 요소가 아직 없는 항목은 NOT_IMPLEMENTED 로 구분 — 대조표가 구현 주장으로 읽히지 않게
- **Status:** complete

### Phase 3: 정리
- [x] `findings.md`·`progress.md` 기록
- [x] 루브릭 G1·G2 판정 갱신(근거 생김 ≠ PASS)
- **Status:** complete

## 하지 않는 것

- 코드 구현. 이 작업은 설계 정본과 대조표까지다.
- PR #238이 건드린 파일 수정. 리뷰 중이므로 충돌을 만들지 않는다.
- 마일스톤 지정. 기존 M0~M5는 RTS 시절 구분이라 맞지 않는다.
