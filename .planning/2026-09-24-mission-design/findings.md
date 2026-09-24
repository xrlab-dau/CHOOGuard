# Findings

## 같은 패턴이 세 번째다 — 제안은 있는데 채택 기록이 없다

오늘 하루에 세 번 같은 구조를 만났다.

| | 제안이 있던 곳 | 없던 것 | 결과 |
|---|---|---|---|
| A-FPS-1 루브릭 | `.planning/2026-09-21-fps-reposition/fps-plan-graph.json` (`version: A-FPS-1-PROPOSED`) | 채택 기록 | 초안에 "Root must record adoption and freeze before evaluating"이라 적혀 있었고, 그대로 멈춰 있었다 |
| 정비 튜토리얼 | 절차 JSON 8단계·실행기·세션·시험이 **이미 구현됨** | 설계 정본 | "설계 문서 없음"이 G9 를 `NOT_EVALUATED` 로 묶고 있었다 |
| 본편 미션 | `FPS_RESEARCH_PROPOSAL.md` — 실행 그래프·결과표·Jev 054·055 판정 | 설계 정본 | G1 이 `plan only` 로 남아 있었다 |

**공통점: 판단은 이미 내려져 있었고 기록만 없었다.** 새로 만들 것이 아니라 채택하면 되는 일이었다. 매번 "없으니 만들자"로 시작했다가 검색 중에 이미 있는 것을 발견했다 — 특히 `MISSION_DESIGN.md` 는 착수 전 grep 한 번으로 재료 전부를 찾았다.

교훈: **`Story_Plan_v5` 와 `.planning/` 을 먼저 훑는다.** 이 저장소는 제안을 많이 만들고 채택 기록을 적게 남긴다.

## G2 대조표는 정말로 없었다

위 셋과 달리 G2 의 source-to-runtime 대조표는 **어느 문서에도 없었다.**

- `FPS_RESEARCH_PROPOSAL.md` — 레퍼런스 → 설계 원리까지. 런타임 열 없음
- `PRODUCTION_GAME_BENCHMARK.md` — 요소별 결과표가 있으나 RTS 런타임(`App/Mvp`) 기준

그래서 `BENCHMARK_FPS_MATRIX.md` 는 채택이 아니라 신규 작성이다. G2 가 요구하는 5축(이동·시선 반응성 / 상호작용 사거리·가시성 / 공간 목표 / 시청각 확인 / 긴장)을 축으로 삼고, 런타임 열에 실제 코드와 실측값을 적었다.

## 두 문서가 독립적으로 같은 결론에 도달했다

`MISSION_DESIGN.md` 8절의 작업 순서는 미션 실행 그래프에서 도출했다(`H` 분기가 중심이므로 NPC 가 먼저).

`BENCHMARK_FPS_MATRIX.md` 7절의 집계는 레퍼런스 대조에서 나왔다.

| 축 | 구현 | 부분 | 미구현 |
|---|---|---|---|
| 1 이동·시선 반응성 | 3 | 0 | 0 |
| 2 상호작용 사거리·가시성 | 4 | 0 | 1 |
| 3 공간 목표 | 0 | 0 | 3 |
| 4 시청각 확인 | 1 | 1 | 3 |
| 5 긴장 | 0 | 1 | 2 |

**빈 항목 대부분이 승객 NPC 와 오디오 하나에 걸린다.** 서로 다른 경로로 같은 결론이 나왔으므로 다음 작업 순서에 대한 확신이 조금 더 높다.

## 확인된 불일치 — 근접 경고 3m vs 상호작용 2.5m

`FireExtinguisherSliceBuilder` 의 근접 경고는 3m 기준이다.

```
[슬라이스] 유닛 간 최소 간격 2.00m · BSN-CONC-FE-004 ↔ BSN-CONC-FE-006
           · 상호작용 상한 3m 안이라 조준이 모호할 수 있습니다.
```

그런데 `FirstPersonResponder.InteractionDistance` 기본값은 **2.5f** 다. 경고가 보수적인 쪽이라 위험하지는 않으나 주석의 "상한 3m"은 현재 값과 맞지 않는다. 실제 조준 모호 여부는 플레이로 확인해야 한다.

## `PRODUCTION_GAME_BENCHMARK.md` 의 RTS 전제 범위

폐기할 문서는 아니다. 운영 루프·자원 경쟁의 비교 근거로 쓸모가 있다. 다만 아래는 1인칭 행위자에 해당하지 않아 `BENCHMARK_FPS_MATRIX.md` 6절에 '가져오지 않음'으로 명시했다.

- JWE3 건물 선택 → 행동 → 대상 지정 (1인칭에는 선택 개념이 없다)
- Planet Coaster 2 / Rescue HQ 관리 오버레이·기지 가용성 (`MVP_DIRECTION.md` 가 RTS 대시보드를 명시적으로 배제)
- Cities: Skylines II 도시 자원망 (범위 밖)
- AoE4 선택 대상 패널

가져온 것은 Two Point Museum 의 파견·귀환(→ `TechnicianDispatch`)과 Firefighting Simulator 의 준비 조건·동료 협업이다.

OpenRA `Wait.cs` 이식본은 **가져오지 않았다.** `App/Mvp/ThirdParty/OpenRaCountdown.cs` 에 있고 RTS 팀 보충에 연결돼 있다. FPS 인계는 `Tick(seconds)` 로 따로 만들었는데, 벽시계 금지 규율 때문에 "승인된 시뮬레이션 시간" 래퍼가 필요 없었기 때문이다.

## 본편의 실제 상태 (2026-09-24 실측)

`App/Fps/` 전체에서 신고·무전·대피·통제·승객이 걸리는 파일은 **전부 튜토리얼 계층의 절차 텍스트와 감사 문구**다. 본편 행동을 구현한 파일은 0 개.

- 신고·무전·전달: 없음
- 승객 NPC: 없음
- 직원 NPC 협업: 없음 (`TechnicianDispatch` 는 상태 기계만, 몸이 없다)
- 접근 통제: 없음
- `AudioSource`: 0건
- 1인칭 이동·시선·상호작용: **있음**

즉 본편이 서 있는 것은 기반뿐이고 그 위에 올릴 미션 동사가 하나도 없다. 설계 문서 두 개가 생겼지만 **G1·G2 모두 `NOT_VERIFIED` 이며 PASS 와는 거리가 멀다.**
