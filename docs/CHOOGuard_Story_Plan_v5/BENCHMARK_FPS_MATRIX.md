# FPS 레퍼런스 → 런타임 대조표

2026-09-24 · CS-EXEC.01.01 · 품질 영역 **G2**의 정본 문서 · 담당 Adrianaline

G2는 레퍼런스에서 가져온 원리가 **실제 런타임 요소로 이어졌는지**를 요구한다.

> A source-to-runtime comparison maps movement/look responsiveness, interaction reach/visibility, spatial objectives, audiovisual acknowledgement and tension from the selected FPS references to station-staff play. … No score for copied combat, resource building or shooter art alone.

기존 두 문서로는 이 요구를 충족할 수 없다. [`FPS_RESEARCH_PROPOSAL.md`](FPS_RESEARCH_PROPOSAL.md)는 조사·제안이고 런타임 열이 없으며, [`PRODUCTION_GAME_BENCHMARK.md`](PRODUCTION_GAME_BENCHMARK.md)는 RTS 전제로 쓰였다(아래 6절). 이 문서가 그 공백을 채운다.

**이 표는 구현 주장이 아니다.** 상태 열이 `NOT_IMPLEMENTED`인 행이 다수다. 그것이 현재 사실이다.

## 1. 축 1 — 이동·시선 반응성

| 레퍼런스 | 확인한 원리 | 런타임 요소 | 상태 |
|---|---|---|---|
| [PUBG 기본 조작](https://support.pubg.com/hc/en-us/articles/360002074913-What-are-the-basic-game-commands) | 간결한 이동 조작 | `FirstPersonResponder.StepInput(...)` — 걷기 1.8 m/s, 달리기 3.4 m/s, 눈높이 1.60m, 몸 반경 0.28m | **구현** |
| 〃 | 시점 조작의 예측 가능성 | `LookDegreesPerPixel=0.09` | **구현** |
| [서든어택 전투 화면](https://guide.sa.nexon.com/guide/31) | 이동 중 주의 배분 — 짧은 상태 표시 | `FirstPersonInteractionHud` 중앙 프롬프트 1개. 별도 패널을 붙이지 않는다 | **구현** |

`StepInput`이 외부 입력을 받는 결정론적 경로라, OS 입력을 합성하지 않고도 시험이 같은 경로를 탄다. G3의 "input traces" 요건과도 맞물린다.

**측정된 값일 뿐 검수된 감각이 아니다.** 1.8 m/s가 역사 규모에서 적절한지는 사람이 플레이해야 안다.

## 2. 축 2 — 상호작용 사거리·가시성

| 레퍼런스 | 확인한 원리 | 런타임 요소 | 상태 |
|---|---|---|---|
| [Half-Life: Alyx](https://www.half-life.com/en/alyx) | 눈앞 물체의 사용 가능성이 보이는 조작. 손이 닿지 않거나 벽 뒤인 대상은 쓸 수 없다 | `FirstPersonResponder.InteractionDistance=2.5f` + 레이캐스트(`QueryTriggerInteraction.Ignore`, 가장 가까운 솔리드 콜라이더만) | **구현** |
| 〃 | VR 손동작 전체를 데스크톱에 강요하지 않는다 | 단일 상호작용 키(E) 경로 | **구현** |
| [Ready or Not 공간 제작](https://voidinteractive.net/vol-82-ready-or-not-development-briefing/) | 문·시야·통로가 판단을 만든다 | 관측 지점을 본체 박스 **밖**·좌우로 벌려 배치(생성기 주석에 실측 근거). 위아래로 쌓으면 30° 내려보기에서 앞면 판이 아래를 가린다 | **구현** |
| [Alien: Isolation](https://blog.playstation.com/2014/10/07/alien-isolation-out-today-on-ps4-ps3/) | 불완전한 정보 — 직접 본 것의 구분 | `FpsGazeTracker` 시선 체류 누적. 관측하지 않은 것은 `FALSE`가 아니라 `UNKNOWN` | **구현**(점검 한정) |
| 〃 | 무전으로 들은 정보 / 확인된 정보의 구분 | — | **NOT_IMPLEMENTED** |

### ⚠ 확인된 불일치

생성기의 근접 경고는 **3m** 기준인데(`유닛 간 최소 간격 … 상호작용 상한 3m 안이라`), 응답자의 `InteractionDistance` 기본값은 **2.5m**다. 경고가 보수적인 쪽이라 위험하지는 않으나 주석의 "상한 3m"은 현재 값과 다르다. 실제 조준 모호 여부는 플레이로 확인해야 한다(`BSN-CONC-FE-004` ↔ `-006`, 간격 2.00m).

## 3. 축 3 — 공간 목표

| 레퍼런스 | 확인한 원리 | 런타임 요소 | 상태 |
|---|---|---|---|
| [PUBG 공간 압박](https://pubg.com/en/game-info/overview) | 시간이 지나면 사용할 수 있는 동선이 달라진다 | — | **NOT_IMPLEMENTED** |
| 〃 | 파란 원이나 임의 체력 감소를 화재 물리로 쓰지 않는다 | (금지 조건. `MISSION_DESIGN.md` 3절과 동일 규율) | 규율 확정 |
| [Ready or Not NPC 경로 변수](https://voidinteractive.net/vol-90-ready-or-not-development-briefing/) | 승객이 못 들었는지, 길이 막혔는지, 일행을 기다리는지 관찰 가능 | — | **NOT_IMPLEMENTED** — 승객 NPC 0 |
| [PUBG 핑](https://pubg.com/en/news/1725) | 가리킨 위치의 공유 | — | **NOT_IMPLEMENTED** |

이 축이 가장 비어 있다. `MISSION_DESIGN.md`의 실행 그래프에서 중심인 `H` 분기(실제 이동이 이어지는가)가 전부 여기에 걸린다.

## 4. 축 4 — 시청각 확인

| 레퍼런스 | 확인한 원리 | 런타임 요소 | 상태 |
|---|---|---|---|
| [서든어택 청각·무전](https://guide.sa.nexon.com/guide/48) | 경보·방송·호출을 방향과 함께 전달 | — | **NOT_IMPLEMENTED** — `AudioSource` 0건 |
| 〃 | 한국어 자막 동반 | — | **NOT_IMPLEMENTED** |
| [Firefighting Simulator](https://www.astragon.com/news/detail/firefighting-simulator-the-squad) | 요청이 즉시 완료되지 않는다 — 수신→이동→수행→결과가 보인다 | `TechnicianDispatch`의 `RECEIVED → TRAVELLING → WORKING → COMPLETED`. 완료가 설비 월드 상태를 바꾼다 | **부분 구현** |
| 〃 | 동료가 실제로 이동하고 작업한다 | — | **NOT_IMPLEMENTED** — 기술자에게 몸이 없다(상태 기계만) |
| 〃 | 소방관 장비·권한 전체를 역무원에게 주지 않는다 | `FacilityInspectable.CanFieldRepair` 거부 + `RoleBoundaryViolations` 감점 | **구현** |

`TechnicianDispatch`가 이 축의 유일한 실물이다. 다만 **시청각 확인이 아니라 상태 기계 확인**이다 — 플레이어가 보는 것은 단계 문자열뿐이고 도착하는 사람이 없다.

## 5. 축 5 — 긴장

| 레퍼런스 | 확인한 원리 | 런타임 요소 | 상태 |
|---|---|---|---|
| Alien: Isolation | 모든 위험 위치를 처음부터 알지 못한다 | 절차 게이트가 관측하지 않은 것을 `UNKNOWN`으로 둔다 | **부분 구현**(점검 한정) |
| Ready or Not | 가변적 NPC 반응 | — | **NOT_IMPLEMENTED** |
| PUBG 공간 압박 | 시간을 쓰면 무엇이 바뀌었는지 공간에 보인다 | — | **NOT_IMPLEMENTED** |

**긴장은 벽시계로 만들지 않는다.** 튜토리얼 계층이 이미 "시계는 플레이어가 쥔다"는 규율로 서 있고(시간 초과 실패 조건 없음), 본편도 같은 규율을 쓴다.

## 6. 운영 레퍼런스에서 가져온 것과 가져오지 않은 것

`PRODUCTION_GAME_BENCHMARK.md`는 RTS 전제로 쓰여 대부분 단일 1인칭 행위자에 맞지 않는다.

| 항목 | 판단 |
|---|---|
| Two Point Museum — 파견과 귀환, 자원 경쟁 | **가져옴.** `TechnicianDispatch`의 요청→도착→수행→복귀 주기로 살아 있다 |
| Firefighting Simulator — 준비 조건과 동료 협업 | **가져옴.** 위 4절 |
| OpenRA `Wait.cs` 카운트다운 | **가져오지 않음.** 이식본은 `App/Mvp/ThirdParty/OpenRaCountdown.cs`에 있고 RTS 팀 보충에 연결돼 있다. FPS 인계는 `Tick(seconds)`로 별도 구현했다 — 벽시계 금지 규율 때문에 승인된 시뮬레이션 시간 래퍼가 필요 없었다 |
| JWE3 — 건물 선택 → 행동 → 대상 지정 | **가져오지 않음.** 세계의 건물을 클릭하는 조작은 1인칭 행위자에 해당하지 않는다 |
| Planet Coaster 2 / Rescue HQ — 관리 오버레이, 기지 가용성 | **가져오지 않음.** RTS 대시보드는 `MVP_DIRECTION.md`가 명시적으로 배제한다 |
| Cities: Skylines II — 도시 자원망 | **가져오지 않음.** 범위 밖 |
| AoE4 — 선택 대상 패널 | **가져오지 않음.** 1인칭에는 선택 개념이 없다 |

G2 지문의 `No score for copied combat, resource building or shooter art alone`에 따라, PUBG·서든어택에서 **전투 메커닉은 하나도 가져오지 않았다.** 가져온 것은 이동·시점·정보 구분·핑 개념뿐이다.

## 7. 집계

| 축 | 구현 | 부분 | 미구현 |
|---|---|---|---|
| 1 이동·시선 반응성 | 3 | 0 | 0 |
| 2 상호작용 사거리·가시성 | 4 | 0 | 1 |
| 3 공간 목표 | 0 | 0 | 3 |
| 4 시청각 확인 | 1 | 1 | 3 |
| 5 긴장 | 0 | 1 | 2 |

**축 1·2는 서 있고, 축 3·4·5는 거의 비어 있다.** 비어 있는 항목 대부분이 승객 NPC와 오디오 하나에 걸려 있다 — `MISSION_DESIGN.md` 8절의 작업 순서가 1번 NPC, 3번 오디오인 것과 일치한다.

## 8. 현재 판정

**G2: NOT_VERIFIED.** source-to-runtime 대조표가 생겨 평가할 형식은 갖췄으나, G2 PASS는 이것 말고도 *"Independent native review confirms actual embodied choices and readable consequences"* 를 요구한다. 독립 검토도, 그 검토가 확인할 플레이도 아직 없다.

대조표가 채워졌다고 PASS가 아니다 — **미구현 항목이 채워지고 사람이 검토해야 한다.**
