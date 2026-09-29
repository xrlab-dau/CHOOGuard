---
trigger: always_on
description: FPS 비상상황은 정해진 목록에서 고르지 않는다. 지금 역 안의 특정 사람·물건에서 만든 원자 전이 후보를 JEV가 고르고 크기를 정해 매번 합성한다. 사건 종류·장소·시각·개수를 고정 가정하는 기능·시험을 만들지 않는다.
---

# 하드룰 — 비상상황은 JEV가 합성한다 (2026-09-26 사용자 정정, 2026-09-29 재확인)

> “jev가 고르는게 아니라 jev가 만드는거야 고른다는건 이미 정해진 비상상황이란거잖아” — 사용자 원문, [결정 원장 5차](../../docs/CHOOGuard_FPS_Prior_Research_20260925/DECISIONS_AND_DEVELOPMENT_STRUCTURE.md)

## 현재 동작

- 비상상황 목록·시나리오 ID·사건 대본은 없다. [`IncidentDirector`](../../Assets/ChooGuard/App/Fps/Emergency/IncidentDirector.cs)가 10~16초마다 **지금 역 안에 있는 특정 승객·물건·설비**에서 물리적으로 가능한 원자 전이 후보(최대 12개)를 만든다. JEV는 무엇이 일어날지(또는 아직 아무 일도 없을지)를 Choice로 고르고, 크기를 Score로 정한다.
- 이미 벌어진 일의 전개(불 확대·연기 확산·경보·쓰러짐·신고·여진 등)도 같은 방식으로 합성한다. ‘화재’·‘의심 물체’는 이 연쇄의 결과로 드러나는 이름이다.
- 그래서 종류·장소·시각·크기·전개가 근무마다 다르고, 근무당 한 번은 별개 사건이 겹칠 수 있다. 장소는 역 전체다. KTX 객실 안, 에스컬레이터 위, 승강장, 3층, 맞이방 모두 해당한다.
- JEV는 코드가 만든 후보를 고르고 점수를 매길 뿐 새 사건을 지어내지 않는다([`JevClient`](../../Assets/ChooGuard/App/Fps/Emergency/JevClient.cs)). 키(`TYPESAFE_API_KEY` 또는 `~/.chooguard/typesafe.key`)가 없으면 같은 후보를 로컬 가중치로 고른다. 목록 추첨으로 바뀌는 것이 아니라 JEV 판단만 대신한다.
- 규범 설계: [DESIGN.md](../../docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/DESIGN.md) §1, [NPC_SCENARIO.md](../../docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/NPC_SCENARIO.md) §7.

## 지켜야 할 것

1. **종류를 닫힌 목록으로 가정하지 않는다.** 지도·길 안내·HUD·무전 같은 기능은 `Hazard` 공통 정보(`Position`·`Where`·`Label`·`Clearance`·`DangerRadius`·`Active`)로 동작해야 한다. 위험 범위·접근 거리처럼 여러 기능이 쓰는 값은 종류별 `is` 분기 대신 `Hazard` 공통 API에 둔다. 종류별 분기가 꼭 필요하면 모르는 종류에서도 안전한 기본 동작을 둔다.
2. **장소를 가정하지 않는다.** 특정 층·지점·승강장에서만 사건이 난다고 가정하지 않는다. 상세 지도가 없는 층이나 열차 안에서 난 사건도 다룰 수 있어야 한다.
3. **시각·개수를 가정하지 않는다.** 첫 사건 시각은 정해져 있지 않고, 사건이 둘 이상 동시에 있을 수 있다.
4. **플레이어가 인지하지 않은 사건·위험을 드러내지 않는다.** 지도 표식뿐 아니라 경로 모양이나 ‘경로 없음’ 같은 안내 문구도 인지한 정보만으로 만든다([INTERACTION_TUTORIAL.md](../../docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/INTERACTION_TUTORIAL.md) §8).
5. **부가 기능이 합성 결과를 바꾸지 않는다.** `StationWorld.Random`은 후보 섞기·로컬 선택·크기를 정하는 난수다. UI·안내 같은 부가 기능이 이 난수를 직접 쓰거나 간접으로 소비하면(예: `StationWorld.Via`의 계단 차선 흔들기) 설정 하나로 이후 합성되는 비상상황이 달라진다.
6. **시험은 합성되는 사건으로 덮는다.** 규칙 단위 시험에서 특정 `Hazard`를 직접 만드는 것은 괜찮다. 기능이 실제 비상상황에서 동작하는지는 `IncidentDirector`가 실제로 만드는 후보(객실 안 과열, 에스컬레이터 넘어짐, 출입문 끼임, 방치 가방, 지진 등)로 여러 종류·장소를 덮어 검사한다. 같은 seed라도 같은 사건을 보장하지 않으므로([DESIGN.md](../../docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/DESIGN.md) §4.3) 특정 사건을 기대값으로 두지 않고 불변조건을 검사한다.

## 금지

- 비상상황 목록·시나리오 ID 테이블, ‘사건군 선택 → 매개변수 추첨’ 파이프라인, 사건별로 미리 정한 위험 구역·안내 경로.
- 이 규칙과 다른 옛 문서를 FPS 본게임의 근거로 쓰는 것. 해당 문서: [NPC_RANDOM_SCENARIOS.md](../../docs/CHOOGuard_FPS_Prior_Research_20260925/NPC_RANDOM_SCENARIOS.md) §3, `CHOOGuard_Clean_Start_v3`·`CHOOGuard_Architecture_v1`의 ‘사건 문법 표본화’.
