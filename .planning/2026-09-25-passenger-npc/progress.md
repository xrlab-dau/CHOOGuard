# Progress Log

## Session: 2026-09-25

### Current Status
- **Phase:** 첫 조각 완료. 브랜치 `feature/tutorial-playable`.
- PlayMode **75 통과 · 0 실패 · 2 건너뜀**(캡처 하네스 2건).
- 승객이 **25.1m 걸어 도착**(직선 23.4m · 20.0초 · `ARRIVED`). 프레임 확인.
- G1 은 여전히 `NOT_VERIFIED` — 이 조각은 미션이 아니라 미션의 재료다.

### 만든 것

| 파일 | 역할 |
|---|---|
| `App/Fps/World/StationNavigation.cs` | DotRecast 질의. 실패 사유 6종 구분 |
| `Editor/StationNavigationBaker.cs` | 대합실 navmesh 굽기 (폴리곤 2,572) |
| `App/Fps/World/PassengerAgent.cs` | 경로 추종 · 상태 4종 |
| `Editor/PassengerBuilder.cs` | 승객 1명 배치. 경로가 나오는 쌍을 실제로 물어본 뒤 씀 |

### 막혔던 곳
첫 베이크에서 **모든 목적지가 `partial_path_target_unreachable`** 이었다. 사유에 폴리곤 수를
실어 보니 `polys=1` — 시작 지점이 폴리곤 한 개짜리 섬이었다. 785㎡ 에 폴리곤 6,328개.
천장을 대역에서 빼고 격자를 거칠게 바꿔 2,572개로 줄이자 풀렸다. 상세는 `findings.md`.

**이유 문자열에 수치를 실은 것이 결정적이었다.** 분류만 있었으면 목적지를 탓했을 것이다.

### 규율 지킨 것
- 길찾기를 직접 구현하지 않고 이미 있는 DotRecast 를 썼다.
- `Mvp`(구 RTS) 계층에 의존하지 않았다.
- 좌표를 계산으로 고정하지 않고 **실제로 경로가 나오는지 물어본 뒤** 썼다.
- 단언만으로 "움직였다" 고 하지 않고 이동 중 프레임을 남겼다.
- 제자리 맴돎을 막으려 이동 거리와 직선 거리를 따로 단언했다.

### 하지 않은 것
유도 · 못 들음 · 동행 대기 · 여럿 · 서로 피하기 · 몸(캡슐이다) · 콜라이더.
`STUCK` 은 코드 경로만 있고 실제로 발생시켜 본 적이 없다.

### Next
유도(플레이어가 가리켜 보내기)와 못 들음·동행 대기. `MISSION_DESIGN` 8절 1순위의 나머지 절반.
