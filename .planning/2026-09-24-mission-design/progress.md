# Progress Log

## Session: 2026-09-24

### Current Status
- **Phase:** Phase 1~3 완료. 커밋하지 않음(로컬 변경).
- 문서 2종 작성: `MISSION_DESIGN.md`(G1 정본), `BENCHMARK_FPS_MATRIX.md`(G2 정본).
- 루브릭 G1·G2 판정 갱신. **둘 다 여전히 `NOT_VERIFIED`** — 평가할 대상이 생겼을 뿐 구현이 아니다.
- 코드는 한 줄도 건드리지 않았다. PR #238 이 리뷰 중이라 충돌을 만들지 않기 위함이다.

### Actions Taken
- **착수 전 실측.** `App/Fps/` 에서 본편 행동 코드가 0 개임을 확인(신고·무전·대피·통제·승객이 걸리는 파일은 전부 튜토리얼 계층). NPC 0, `AudioSource` 0.
- **Phase 1 (B)** `MISSION_DESIGN.md` 작성. 새로 창작하지 않고 `FPS_RESEARCH_PROPOSAL.md` 의 실행 그래프·플레이 선택 결과표와 Jev 054·055 판정을 채택 기록으로 고정. 첫 미션은 역사 화재·대피(0.93/conf 0.89). G1 이 요구하는 재난 1종·테러 1종·변형 3종과의 대응표 작성. 완료 판정 규율(월드 상태, 버튼·타이머 아님)을 튜토리얼 계층에서 승계.
- **Phase 2 (A)** `BENCHMARK_FPS_MATRIX.md` 신규 작성. G2 의 5축에 레퍼런스 원리와 런타임 요소를 대응. 런타임 열은 코드 실측값으로 채움(`WalkSpeed=1.8`, `EyeHeight=1.60`, `InteractionDistance=2.5` 등). RTS 전제 항목은 '가져오지 않음'으로 명시.
- **Phase 3** `findings.md`·`progress.md` 기록, `QUALITY_A_RUBRIC.md` G1·G2 갱신.

### 결과 집계 (BENCHMARK_FPS_MATRIX.md 7절)

| 축 | 구현 | 부분 | 미구현 |
|---|---|---|---|
| 1 이동·시선 반응성 | 3 | 0 | 0 |
| 2 상호작용 사거리·가시성 | 4 | 0 | 1 |
| 3 공간 목표 | 0 | 0 | 3 |
| 4 시청각 확인 | 1 | 1 | 3 |
| 5 긴장 | 0 | 1 | 2 |

합계 구현 8 · 부분 2 · 미구현 9.

### Errors
| Error | Resolution |
|-------|------------|
| "미션 설계를 새로 써야 한다"고 판단하고 착수 | 착수 전 grep 에서 `FPS_RESEARCH_PROPOSAL.md` 가 실행 그래프·결과표·Jev 판정을 이미 갖고 있음을 발견. 창작이 아니라 채택으로 방향 전환 |
| G2 도 기존 문서를 채택하면 될 것이라 예상 | 두 문서 모두 런타임 열이 없어 채택할 대상이 없었다. 신규 작성으로 전환 |

오늘 하루에 "제안은 있는데 채택 기록이 없다"를 세 번 만났다(A-FPS-1 루브릭, 정비 튜토리얼, 본편 미션). 상세는 `findings.md`.

### Next Step
`MISSION_DESIGN.md` 8절 순서를 따른다. `BENCHMARK_FPS_MATRIX.md` 집계와도 일치한다.

1. **승객 NPC 와 유도** — 실행 그래프의 `H` 분기(실제 이동이 이어지는가)가 미션의 중심이고, 대조표 축 3 이 통째로 여기 걸린다. 못 들음·막힘·동행 대기를 구분할 수 있어야 한다.
2. **신고·전달과 복창** — 잘못 전달된 위치가 복창에서 드러나는 것이 학습 지점이다.
3. **오디오** — 축 4 의 미구현 3 건이 여기 걸린다. G3 요건이기도 하다.
4. 직원 NPC 협업과 인계 — `TechnicianDispatch` 에 몸을 주는 작업과 함께.
5. 이월 연결 — 본편이 `TutorialCarryoverStore` 를 읽어 초기 조건으로 삼는다.

### 남은 확인거리
- 근접 경고 3m 과 `InteractionDistance` 2.5m 불일치. 실제 조준 모호 여부는 플레이로 확인.
- `PRODUCTION_GAME_BENCHMARK.md` 는 폐기하지 않았다. RTS 전제임을 표시할지, 운영 루프 참고 자료로 그대로 둘지 미정.
