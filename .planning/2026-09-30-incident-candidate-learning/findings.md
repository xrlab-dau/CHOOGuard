# 관측 기록

## 착수 전에 확인한 것 (2026-09-30)

### graphify 그래프가 짚어 준 것
`.agents/rules/graphify.md` 가 `always_on` 이라 먼저 조회했다(오늘 그 전까지 지키지 않았다).
그래프(24,407 노드)가 내가 몰랐던 자료 둘을 반환했다.

- `.planning/2026-09-26-world-npc-incidents/plan.md` — 트윈 실측과 구현 순서, **남은 것**
- `.planning/2026-09-28-world-interaction/research.md` — 설비 조작 자료조사(열차·출입문·개집표기·
  에스컬레이터·엘리베이터·소방설비·AED)

### 오늘 내가 발견한 '성분 단절' 은 이미 기록돼 있었다
`2026-09-26` 계획의 **남은 것** 에 남측 우물 계단참 단절이 적혀 있다 — 회랑이 머리마다 원본 벽
(`MainShell_5·7`)으로 칸이 나뉘고 남쪽은 길이 56 m·높이 4 m 유리벽(`MainShell_6`)이 막는다.
실제 접속을 정할 자료가 없어(SouthGate 영상 정합 NO-MATCH) 원본 벽을 자르지 않았다고 한다.

**즉 내가 #242 조사에서 본 '성분 1 vs 성분 3' 분리는 원본 모델의 구조이고 배치 결함이 아니다.**
그래프를 먼저 조회했다면 그 조사에 쓴 시간을 아꼈을 것이다.

### 지금 후보 생성이 읽는 것
`IncidentDirector.Origins()` (L388) 가 쓰는 조건:

    CarriesPowerBank && Settled      → 과열
    Activity != Walk || Elderly      → 쓰러짐
    Luggage == 2 && Settled          → 가방 방치
    escalator.Carries(body)          → 넘어짐
    Train.Stage == Closing, 문 1.5m  → 끼임
    (무조건)                          → 지진

여섯 종이고 전부 손으로 쓴 조건이다. 구역별로 고르게 뽑되(`Spread`) 최대 12개로 자른다.

### 학습 데이터 경로
- `EmergencySession.cs:136` — `jev-<UTC>.jsonl`, 요청마다 `state`·`questions`·`answers` 전체
- `ShiftLog.Compositions` — `t·kind·key·magnitude·by·detail`
- 키는 오늘 유효 확인됨(`GET /v1/models` → HTTP 200, `jev-latest`·`jev-preview`)

**아직 한 번도 실제 근무를 돌려 로그를 받은 적이 없다.** 표본이 몇 건 쌓이는지 모른다.
