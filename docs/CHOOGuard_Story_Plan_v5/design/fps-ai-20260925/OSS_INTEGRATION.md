# OSS·도구 적용 정밀설계

기준일: 2026-09-25. **초기 설계 선택에 대한 구현/검증이 진행됐으며 전체 수용은 미완료다.** 최신 실행 증거와 한계는 [EXECUTION_PLAN.md §0](EXECUTION_PLAN.md#0-구현검증-체크포인트--2026-09-25)가 우선한다.
상위 계약은 [DESIGN.md](DESIGN.md), NPC·사건 의미는 [NPC_SCENARIO.md](NPC_SCENARIO.md), 실행 순서는 [EXECUTION_PLAN.md](EXECUTION_PLAN.md)를 따른다.
사용자 결정은 [결정 원장](../../../CHOOGuard_FPS_Prior_Research_20260925/DECISIONS_AND_DEVELOPMENT_STRUCTURE.md)에 따른다.

## 1. 적용 원칙과 확인 수준

- 제품은 **FPS 기반 비상상황 대응 시뮬레이션 게임**이다. 인간 1명이 정비·청소·승무·역무를 전환하고, 별도 튜토리얼과 수백 명의 개별 자율 NPC가 있는 본게임을 갖춘다. 인간 멀티플레이, 사격전, 엔진 교체는 이 선택의 요구사항이 아니다.
- Unity GameObject·PhysX·Input System·uGUI/TMP를 유지한다. Windows x64/Mono 개발 Player의 빌드와 실제 실행 의무도 유지한다.
- 현재 상태·플레이어 개입·NPC 관측/목표에서 후보를 동적으로 구성하고 JEV 추론과 공통 인과/실행 규칙을 반복해 미래를 합성한다. 완성 사건 pack이나 결말 목록은 런타임 미래의 공급원이 아니다. `NpcAgentRuntime`과 `FutureComposer`는 게임 코드가 구현할 책임이지 JEV 내장 에이전트 프레임워크가 아니다.
- **유지**: 현재 선언/코드에 있는 구성. **도입 후보**: 구체 채택 방향은 선택했지만 호환 spike·설치가 남음. **참고 전용**: 원리만 읽고 런타임 의존성을 추가하지 않음. **보류**: 현재 최소 구성에 필요하지 않음.
- manifest/lock은 의존성 선언·해결 근거, DLL/metadata는 로컬 존재 근거, 소스는 사용 경로 근거다. 어느 것도 해당 Windows Player의 작동 증명은 아니다.
- `graphify-out/GRAPH_REPORT.md`는 2026-09-21 자료로만 참고했다. 아래 판단은 현재 코드·manifest를 우선한다.
- 라이선스는 조사 제외 필터가 아니다. 연구 열람과 제품 재배포를 분리하고, 배포 시 코드·native binary·모델·콘텐츠별 조건을 확인한다.
- 아래 §2 이후의 초기 설치/ABI 관찰은 설계 착수 시점의 기록이다. 이후 macOS/Windows용 Python 3.13.15·SQLite·Node 24.21.0 runtime package를 조립하고 macOS native 물리/SQLite/브로커와 별도 PPO 4096 step을 실행했다. deprecated/wrapt 충돌은 1.2.18/1.17.3으로 해소했다. Windows Player·실제 JEV/대화 제공자 수용은 미실행이다. 자세한 출처·hash·라이선스 및 Cloud 재조립 경로는 [worker README](../../../../workers/physics/README.md)를 따른다.

## 2. 설계 착수 시점의 구성: 재사용한 기반

| 근거 | 확인된 버전·내용 | 확인되지 않은 것 |
|---|---|---|
| `ProjectSettings/ProjectVersion.txt` | Unity `6000.3.23f1` | 이 문서에서 Editor/Player 실행하지 않음 |
| `Packages/manifest.json` | URP `17.3.0`, Input System `1.20.0`, uGUI `2.0.0`, TMP `5.0.0`, Test Framework `1.6.0` | 새 NPC/상세조작 결합 성능 |
| 같은 manifest | `com.unity.modules.physics`, `.ai`, `.jsonserialize` 각각 `1.0.0` | 모듈 버전은 PhysX upstream 버전이 아님; `.ai`는 AI Navigation UPM 설치 증거가 아님 |
| 같은 manifest/lock | NuGetForUnity `v4.5.0`, git hash `a7c6b49a0141a5bff9b1983e38137522ef61977d` | Windows 패키징 성공 |
| `Assets/packages.config` | `DotRecast.Core`, `.Detour`, `.Recast` 모두 `2026.3.1`, `netstandard2.1` | `.Detour.Crowd`는 설치 선언 없음 |
| `Assets/Packages/DotRecast.Core.2026.3.1/DotRecast.Core.nuspec` | upstream `ikpil/DotRecast`, commit `16976369bebab455b59bcb93c799c775a91d816d` | 최신 버전이라는 주장 아님 |
| `Packages/packages-lock.json` | `com.unity.nuget.newtonsoft-json@3.2.2`, Performance Testing `3.5.0`은 전이 의존성 | 실제 게임 코드가 모든 기능을 사용한다는 뜻 아님 |
| 로컬 Newtonsoft package metadata | UPM `3.2.2`는 Newtonsoft.Json `13.0.2`에 동기화 | JsonUtility의 엄격한 대체 검증기가 자동 활성화된 상태 아님 |
| `workers/physics/requirements-core.txt` | JuPedSim `1.4.2`, NumPy `2.5.3`, Shapely `2.1.2`, deprecated `1.3.1`, wrapt `2.4.1`, fdsreader `1.11.9`, typing-extensions `4.16.0` 고정 선언 | 전체 dependency closure·Windows wheel 조합 미검증 |
| `workers/physics/.venv/pyvenv.cfg` 및 JuPedSim dist-info | CPython `3.12.11` macOS arm64 환경, JuPedSim `1.4.2` 로컬 metadata | macOS 환경을 Windows에 복사해서 사용할 수 없음 |
| `workers/physics/upstream-lock.json` | pyFDS-Evac commit `4e4b5f2b650b822e84409e68d2f27379b7a2aebf`, 원본 모듈 3개별 SHA-256 | 전체 GUI/runner 설치 아님 |
| `workers/prediction/jev-proxy.mjs` | Node built-in HTTP/HTTPS 사용; Vercel `/turnaround` 전용 공개 경로 | `@typesafe-ai/sdk`를 설치했다는 증거 아님 |

현재 UPM/lock/NuGet/worker 선언에서 AI Navigation UPM, XRI, GOAP, ML-Agents, Scenic, Gymnasium, SB3, PettingZoo의 설치 근거는 없다.
`com.unity.multiplayer.center@1.0.1`가 있다고 NGO/Mirror/FishNet 또는 인간 네트워크 플레이가 설치된 것으로 해석하지 않는다.

## 3. 최소 채택 매트릭스

| 상태 | 도구·정확한 대상 | 역할·선택 이유 | 경계·필수 확인 |
|---|---|---|---|
| 유지 | 기존 `FirstPersonResponder`, Input System `1.20.0` | 이미 있는 이동·시선·가시 대상 탐색을 확장 | 새 FPS template/DOTS로 교체하지 않음; 손 조작 중 이동/시점·UI focus 확인 |
| 유지 | GameObject CharacterController, PhysX Rigidbody/joints | 사람 이동, 문·서랍·도구의 충돌/제약 | 시각 pose와 작업 사실 분리; 미세 나사산·실제 체결 토크 솔버라는 주장 금지 |
| 유지 | uGUI `2.0.0` + TMP `5.0.0` | native HUD·도구 피드백·한국어 대화 | 웹 UI/XR UI 재기반화 없음; 실제 Player 가독성과 입력 확인 |
| 유지 | DotRecast Core/Detour/Recast `2026.3.1` | 기존 베이크와 경로검색 자산·코드 재사용 | 현재 2층 전용 제약을 층·포털·문 가용성으로 확장하는 spike |
| 도입 후보 | `DotRecast.Detour.Crowd`, 같은 `2026.3.1` 소스 계열 | 일반 NPC의 경로 추종·국소회피를 같은 계열로 구성 | 소스 csproj 확인, 설치/배포 package 검증 전; 별도 NavMeshAgent 중복 구동 금지 |
| 보류 | `com.unity.ai.navigation`, 선행조사 문서판 `2.0.15` | DotRecast 확장 실패가 관찰될 때의 비교군 | 현재 설치 아님; 선택하면 해당 경로 backend를 완전 교체하고 이중 베이크 체계 제거 |
| 유지 | JuPedSim `1.4.2` + 현 Python worker | 제한된 과학 보행 profile·기준 비교 | 실역 geometry 검증 전에는 기준 공간 전용; 일반 gameplay mover와 위치 소유권 분리 |
| 참고 전용 | `crashkonijn/GOAP` | 목표·다단계 행동·중단/재계획 원리 참고 | 자율 계획은 필수지만 이 외부 planner의 도입 필요는 별도 입증; 버전 미고정 |
| 참고 전용 | `Unity-Technologies/XR-Interaction-Toolkit-Examples`, 조사 README XRI `3.4.0`/Unity `6000.3` | joint 기반 door/drawer/key/socket 조작 원리 | 데스크톱 입력으로 독립 구현; XRI·OpenXR·XR loader 설치 안 함 |
| 참고 전용 | `MarvinBeym/MscModApi`, `leezer3/OpenBVE`, `openrails/openrails` | 부품 설치/체결, 문 interlock, 차량/제동 상태 구분 | 게임 DLL·운전 시뮬레이터·한국 철도 수치의 임의 이식 금지 |
| 선택적 연구 후보 | `Scenic-Foundation/Scenic@v3.1.1`, PyPI `scenic==3.1.1` | 오프라인 초기 조건·제약·holdout 표본 생성 | 공식 태그 metadata 확인, 미설치; live 미래 대본/전이 공급원이나 core loop 선행조건 아님 |
| 도입 후보 | TypeSafe direct `/v1/systemone`, 모델 `jev-1.13.0` | 동적으로 구성한 NPC 목표/행동 및 다음 전이 후보의 typed 추론 | `/npc/decision`과 `/future/step`은 별도 계약·공통 예산; 기존 Vercel turnaround와 별개 |
| 유지 | 기존 Vercel `typesafe-ai/jev` `/turnaround` | 운영 복귀 workload의 현 기능 보존 | 시민 행동용 재명명·자동 direct fallback 금지 |
| 도입 후보 | 별도 `DialogueProvider` 경계; 공급사·모델 미선정 | JEV가 제공하지 않는 한국어 생성 대화 | 언어/사실/지연 평가 후 한 provider를 고정; JEV text-generation 흉내 금지 |
| 도입 후보 | `gymnasium==1.2.0` + `stable-baselines3==2.7.0`의 PPO | 별도 정책학습의 단순한 첫 실험기 | 공식 태그 의존성 확인; Python 격리환경, Player에 trainer 미포함 |
| 보류 | `com.unity.ml-agents` + `mlagents`/`mlagents_envs`; `Farama-Foundation/PettingZoo` | 전자는 Unity sensor/action 학습, 후자는 실제 MARL API 필요 시 | 설치 아님; 아래 선택 사유 없이 추가하지 않음 |
| 유지·확장 | SQLite + `SqliteProvider`/`SqliteRunStore`, 기존 JSON Schema | 기억·실행 결과·재개, 경계 형식 검증 | Windows ABI 구현 필요; 새 DB/vector DB/ORM 도입 안 함 |
| 유지 | Unity Profiler, Test Framework `1.6.0`, Performance Testing `3.5.0` | 실제 프레임·물리·지연·PlayMode 관찰 | 테스트 개수나 Editor 평균만으로 수백 NPC·Windows 완료 주장 금지 |

이 표의 도입 후보는 설치 승인이나 검증 완료가 아니다. 연구용 Scenic/learner는 게임 기본 실행의 새 의존성이 되지 않는다.

## 4. FPS·세부 조작: 기존 코드에 붙이는 범위

`Assets/ChooGuard/App/Fps/FirstPersonResponder.cs`의 CharacterController·입력·시선 경로를 유지하고, 행동 executor가 현재 상호작용의 의미를 확장한다.
`ProcedureRunner`/`RuleTruth`는 절차·사실 판정의 재사용 대상이며, GOAP나 XRI의 성공 callback으로 대체하지 않는다.

| 참고 기능 | 데스크톱 매핑 설계 | authoritative 결과 |
|---|---|---|
| XRI 잡기/velocity tracking | 시선으로 대상 선택, 누름 유지로 잡기, 마우스 이동으로 제한된 목표 pose | 실제 도달 pose·충돌·손/도구 점유 상태 |
| XRI hinge/slider/knob | 조작 모드에서 마우스 delta를 축/각도 목표로 변환, 해제하면 조작 종료 | 문 개방 정도·축 제한·잠김·장애물 상태 |
| XRI socket/key | 접근 거리·정렬·종류·점유를 확인하고 삽입 동작 수행 | 호환되는 대상에 실제 결합; 단순 hover로 완료하지 않음 |
| MscModApi Part/Screw | 장착 가능, 장착됨, 체결됨, 검사됨을 다른 상태로 유지 | 공구 규격·부품/체결점 ID·선행조건·측정/검사 증거 |
| OpenBVE 문 상태 | 열기 요청, 개방 정도, interlock, 고장/장애물, 차량 집계를 분리 | 실제 문/차종 자료의 조건이 우선; 원본 임계값 복사 안 함 |
| Open Rails 제동 상태 | 단위·부품 상태·진단 표현의 구조만 참고 | 실제 차량 모델·작업 매뉴얼 없이 제동 안전성 계산으로 사용 안 함 |

- 손 목표점을 transform으로 강제 이동시키면서 같은 물체를 joint가 제어하는 충돌을 피한다. 선택한 grab 모드마다 pose writer를 하나로 둔다.
- 숫자가 없는 자료는 `미확인`으로 남긴다. 마우스 회전량·체결 횟수는 게임 조작량이지 자동으로 실제 토크가 아니다.
- 새 FPS template, Entities Physics, AGX, Obi, 인간 multiplayer stack은 현재 세부 조작을 완성하는 필수 도구가 아니므로 추가하지 않는다.
- XRI/MscModApi에서 코드 일부를 실제 가져올 경우 파일·판본·수정 범위·권리 고지를 기록한다. 지금 선택은 통째 import가 아니다.

## 5. 이동: DotRecast 기본선과 단일 소유자

### 5.1 현재 경로 재사용

`Assets/ChooGuard/App/Mvp/MvpTeamNavigation.cs`는 DotRecast Detour 경로를 반환한다.
`Assets/ChooGuard/Editor/MvpTeamNavigationBuilder.cs`는 같은 계열로 실제 mesh를 베이크·직렬화한다.
현재 경로는 시작/종료 y≈5.05의 2층만 허용하고, off-mesh/층간 연결이 없으며 partial path를 거부한다.
따라서 이미 있는 이 경로를 버리고 Unity AI Navigation을 함께 설치하는 것은 최소 변경이 아니다.

### 5.2 선택한 책임 분리

1. 일반 gameplay: DotRecast route/crowd가 이동 목표·제안 pose를 계산하고 Unity 로컬 locomotion adapter 한 곳이 pose를 반영한다.
2. 과학 profile: 검증된 geometry의 JuPedSim worker가 보행 pose를 계산하고 승인된 snapshot을 Unity가 렌더링한다.
3. 공통 world writer는 actor ID·run/generation·tick·geometry/route revision을 검사한다. JEV는 위치를 쓰지 않는다.
4. 어느 profile도 transform, Animator root motion, CharacterController, NavMeshAgent가 같은 actor를 동시에 이동시키지 않는다.
5. 예약된 좁은 작업점·도구 접근·도움 대상 주변 공간은 행동 자원 규칙이다. 국소회피가 업무 예약/동의 문제를 해결하지 않는다.

`DotRecast.Detour.Crowd`의 `netstandard2.1` csproj가 로컬 upstream 소스에 있으므로 먼저 같은 릴리스의 사용 가능성을 확인한다.
NuGet package/assembly 의존성과 Windows Mono 로드를 확인한 뒤 좁은 출입문·교행·정지 작업자·움직이는 플레이어를 함께 실험한다.
현재 `TryPlan`의 고정 배열 할당은 고빈도 NPC replanning으로 그대로 확대하지 않고, 실제 부하 측정 뒤 재사용 버퍼·경로 변경 이벤트 중심으로 정리한다.

### 5.3 확장/교체 판정

- 먼저 실제 층별 통행 geometry, 문 닫힘/잠김, 계단·승강기·열차 출입 포털의 연결 및 접근 자격을 정의한다.
- 새 geometry에 도달점 투영 오차·단절 경로·경사/폭·동적 차단을 검증한다. 기존 2층 성공은 다층 호환 증거가 아니다.
- 일반/과학 profile을 실행 중 전환해야 한다면 마지막 승인 pose에서 기존 solver를 정지하고 새 solver의 삽입 승인을 받은 뒤 소유권을 넘긴다.
- 인계 실패 시 기존 위치에서 `BLOCKED`로 남긴다. 새 solver에 삽입할 수 없다고 다른 위치로 순간이동하거나 인원을 삭제하지 않는다.
- Unity AI Navigation 비교는 DotRecast 확장상의 구체 실패가 있을 때만 한다. 선택 시 동일 route contract 아래 해당 backend와 베이크 산출물을 cutover한다.
- JuPedSim `Exit`의 solver 제거와 게임의 집결 확인/인계는 다르다. actor의 기억·관계·지원 상태는 solver 제거 후에도 남는다.

## 6. GOAP를 지금 설치하지 않는 이유

행동 실행은 `REQUESTED→RESERVED→APPROACHING→EXECUTING→VERIFYING→COMPLETED`와 `BLOCKED/CANCELLED/FAILED`로 명시한다. 이 lifecycle만으로 자율성이나 계획이 구현됐다고 보지는 않는다.
읽기 전용 즉시 관찰은 예약/이동을 생략할 수 있으나 실제 작업의 완료 증거는 생략할 수 없다.
`NpcAgentRuntime`이 욕구·목표·부분 관측·기억·`GoalPlan`을 소유하고, 플레이어 요청 없이도 목표 수립→후보 구성/JEV 추론→행동→결과 관측→재계획·협상을 반복한다. 코드 소유 제어흐름과 JEV의 좁은 typed 판단을 합성하는 구조다.[P6] 별도 agent framework를 자동 추가하지 않는다.

- 기본은 코드 소유의 목표/계획·실행기다. 계획의 자율적 생성·유지·수정은 필수이며, GOAP 미설치를 반응형 NPC나 고정 과업 대본으로 축소하는 이유로 삼지 않는다. 외부 GOAP는 이 책임을 더 잘 구현한다는 비교 근거가 있을 때만 재평가한다.
- 재평가 실험은 동일 목표/자원에서 기존 실행기와 GOAP의 완료율·예약 충돌·취소 후 잔여 점유·replan 비용을 비교한다.
- GOAP를 채택해도 출력은 같은 `ActorActionIntent` 후보이며 world write 권한은 없다. 기존 `CommandIntent`를 시민 행동 bus로 오인하지 않는다.
- `HaulItemAction`의 timer 완료·데모 inventory·scene 탐색을 실제 부품 인계나 작업 검수로 복사하지 않는다.

## 7. JEV direct·동적 미래·생성 대화의 서로 다른 경계

### 7.1 현재 turnaround는 보존

`workers/prediction/jev-proxy.mjs`는 `https://ai-gateway.vercel.sh/typesafe/v1/systemone`, 모델 `typesafe-ai/jev`를 사용한다.
공개 POST는 `/turnaround`, Noul workload 분류이며 1 in-flight·3,000ms upstream deadline·retry 0이다.
이 제한은 현 어댑터의 제한이지 JEV 제품 전체의 제한이 아니다. 같은 파일의 도달 불가능한 과거 forecast 분기를 NPC API라고 재사용하지 않는다.

### 7.2 신규 NPC 판단과 미래 전이 경로

- **선택: direct HTTP API + 기존 Node built-in HTTPS 패턴.** `POST https://api.typesafe.ai/v1/systemone`, 모델 ID `jev-1.13.0`을 명시한다.
- `/npc/decision`은 actor 목표·관측에 근거해 동적으로 구성한 행동 후보의 [NPC wire](contracts/npc-decision.schema.json)를, `/future/step`은 한 단계의 동적 전이 추론 [future wire](contracts/future-step.schema.json)를 따른다. 기존 `/turnaround`의 payload/소비자를 변경하지 않는다.
- 공식 `typesafe-ai/typesafe-sdk-js@v0.6.0`는 wire·primitive·오류 의미의 기준 자료다. 단일 POST 때문에 SDK와 추가 재시도 정책을 동시에 설치하지 않는다.
- 공식 Python SDK 조사판 `0.7.1`도 원격 client이며 로컬 JEV 엔진이 아니다. 이번 구성은 과학 worker에 JEV client를 끼워 넣지 않는다.
- Choice는 현재 구성한 허용 후보 ID를 선택하고, Score/Noul은 명시된 rubric/사건 의미에만 사용한다. 자유 텍스트·코드·임의 수치·도구 인수·완료 사실을 생성하지 않는다. 계산·시간 비교·물리/안전 불변식은 코드와 검증된 solver의 책임이다.[P7]
- request/actor/decisionSeq/hash와 미래의 run/generation/forecastId/branchId/stepSeq 대응은 proxy와 코드가 확인한다. provider가 게임 envelope를 이해하거나 echo한다고 가정하지 않는다. 미래 요청의 불변 snapshot/read set 및 actor/world 관점을 보존하고, 응답은 선택된 후보뿐이지 world patch가 아니다.
- 모델 alias와 gateway 모델 ID를 자동 상호 대체하지 않는다. 응답 모델, 필드, usage와 서버 상태가 다른 경로는 별도 검증한다.
- 관측이 다른 actor를 전체 세계 state에 묶지 않는다. actor별 snapshot 안에서 독립 질문만 batch한다.
- 두 신규 경계는 실험 시작값 **합계 12 dispatch/sec·12 in-flight·pending 128**과 실제 계정 RPM/TPS·비용·deadline을 공유한다. 각각에 한도를 복제하지 않고 foreground/live NPC가 예측 가지에 밀려 굶지 않게 배분한다. 60Hz AI나 수백 NPC 동시 고빈도 호출을 약속하지 않으며 SDK 기본 retry 2회/시도별 10초를 gameplay 기본값으로 가져오지 않는다.
- 지연·429·단절 중에는 로컬 기존 행동을 계속하거나 보류한다. stale 권한/대상 revision 응답을 새 행동으로 적용하지 않는다.
- 개발용 loopback proxy의 key-stdin 방식과 비밀 비로그 원칙을 재사용한다. 제품 공용 API key를 Player/배포 Node 파일에 넣지 않는다.
- 제품 인증키를 서비스가 보유하는 배포 또는 사용자 개인키 방식은 보안/운영 배포 결정으로 명시한다. direct는 provider 경로 선택이지 Player에 비밀을 심는 뜻이 아니다.
- `FutureComposer`는 현재 상태/개입에서 `AffordanceBuilder`가 동적으로 만든 전이 후보를 JEV에 묻고, `TransitionKernel`로 다음 격리 상태를 계산한 뒤 그 상태로 다시 묻는다. `InitialWorldSeed`는 초기 상태, `TransitionDefinition`은 원자적 인과 연산자/제약이며 완성 사건 ID나 미래 대본이 아니다.
- 가상 가지는 실세계 기억·예약·완료 receipt·actor sequence를 변경하지 않는다. 예측 결말을 강제하지 않으며, 즉시 가능한 환경 효과만 최신 상태에서 재검사해 live writer에 제안한다. 가상 플레이어/NPC 선택을 실제 행동으로 자동 실행하지 않는다.
- 실제와 예측은 같은 인과/실행 규칙을 쓴다. solver 결합이 지원되지 않는 효과는 incomplete로 남긴다. JEV의 자유 수치 생성이나 고정 화재장 연장으로 그 공백을 메우지 않는다.
- 재생은 기록된 입력·규칙 판본·모델 결과를 사용한다. seed와 새 원격 요청만으로 동일 결과를 보장하지 않는다. JEV API에 영속 기억·자율 agent loop가 내장됐다는 근거는 없으므로 외부 기억·코드 소유 runtime이 이를 담당한다. 근거는 [모델 조사 §3–5](../../../CHOOGuard_FPS_Prior_Research_20260925/JEV_MODEL_SOURCES.md)와 [P6][P7]이며 이 조합의 실제 게임 품질은 미검증이다.

### 7.3 대화 provider는 아직 제품 선택 전

JEV와 별개의 생성 모델을 쓰는 것은 확정이지만 공급사/모델은 한국어·직무 대화 평가 전에는 임의 선택하지 않는다.
`DialogueProvider`에는 actor가 아는 사실·출처·미확인 사항, 발화 의도, 실제 행동 상태, 관련 기억만 전달한다.
출력은 대사와 근거 참조이지 행동 승인·DB write·작업 완료가 아니다. “했어요”라는 문장으로 VERIFYING을 건너뛰지 않는다.
기한 초과 시 검증된 짧은 상태 대사를 사용할 수 있지만 그것을 자유대화 기능의 완료로 세지 않는다.
선정 비교에는 한국어 자연스러움, 모르는 사실 인정, 관계 지속성, 긴급상황 간결성, 실제 end-to-end 지연, 비용·보관 정책을 포함한다.
JEV의 `shared_truth_flexible_presentation`은 설계 조언이며 이 평가나 안전 판정을 대신하지 않는다.

## 8. Scenic은 선택적 오프라인 초기 조건·제약·holdout 도구

공식 `v3.1.1`의 Python≥3.8 선언과 dependency 목록을 확인했다. `main`의 `3.2.0b1`을 안정 배포판이라고 선정하지 않는다.[P3]
`python-fcl`, Rtree, manifold3d 등 native 의존성이 있으므로 기존 physics `.venv`에 섞지 않고 저작 전용 환경으로 분리한다.

- 입력: geometry/통행 graph 판본, 일상 population·환경/설비 초기 조건, seed, 허용 제약과 분포의 출처 구분.
- 역할: `require`·precondition·invariant로 초기 조건 및 검증용 제약/holdout 표본을 추출한다. 현재 플레이에서 다음에 일어날 사건 순서·결말을 작성하는 역할이 아니다.
- 출력: 생성기/규칙/geometry 버전·seed·채택 조건·거부 사유를 담은 JSON 초기 조건/제약 자료. `InitialWorldSeed`는 시작 상태만 담고 완성 사건 pack을 반입하지 않는다.
- 반입: JSON Schema 형식 검사 뒤 게임의 권한·경로·자원·초기 상태 검사를 통과해야 한다. Scenic acceptance가 현실 안전성 인증은 아니다.
- 본게임의 미래는 현재 상태·플레이어 개입·자율 NPC 행동에서 JEV+`FutureComposer`+공통 `TransitionKernel`이 매 단계 합성한다. Scenic 설치/출력은 이 core loop의 선행조건이 아니며 실패한 현재 세계를 reject-and-resample로 되감지 않는다.
- 선택적 첫 spike는 초기 설비 의존관계·작업점 점유·출구 단절·지원 필요 인구 조건이 제약에 따라 허용/거부되는지와 holdout의 거부 편향을 관찰한다. live 사건의 다음 선택을 Scenic에 위임하지 않는다.
- Unity용 Scenic simulator adapter가 설치된 증거는 없다. 연구를 선택할 경우 JSON export/import 범위를 평가하며 실시간 양방향 adapter는 최소 구성 밖이다.

## 9. 외부 기억과 별도 정책학습

### 9.1 첫 학습기 선택

**Gymnasium `1.2.0` + SB3 `2.7.0` PPO**를 격리된 정책학습 연구의 첫 후보로 선택한다.[P1][P2]
SB3 태그는 Gymnasium `>=0.29.1,<1.3.0`, NumPy `<3`, PyTorch `>=2.3,<3`을 선언한다. Gymnasium 태그는 Python≥3.10이다.
따라서 CPython3.12 기반의 별도 환경 설계는 선언 범위상 가능하지만 Torch wheel/ABI·학습 실행 호환성을 측정한 것은 아니다.
Torch·하위 의존성의 정확한 resolved version과 wheel hash는 첫 설치 spike에서 고정한다. 최신 `master`의 alpha 버전을 현재 안정판으로 쓰지 않는다.

- 첫 실험은 한 동료의 의미 행동 정책이고 나머지 인물은 고정 baseline이다. Gymnasium의 single-agent API가 수백 인물의 존재를 금지하는 것은 아니다.
- 관측은 해당 actor의 당시 정보만, 행동은 코드가 정의한 유한 동사/대상 후보, 전이는 같은 executor의 실제 결과를 사용한다.
- `reset(seed)`는 별도 연구 run을 만들며 `step`은 요청한 행동이 수락/거절되고 실제 시간이 진행된 결과를 반환한다.
- 성공 종료와 시간/모델 범위 초과에 의한 truncation을 구분한다. FDS 범위 종료를 대피 성공 보상으로 바꾸지 않는다.
- PPO 기본형에 action-mask 지원을 가정하지 않는다. 금지 행동은 공통 validator가 거부하고 거부 결과를 학습에 돌려준다.
- 보상은 작업 증거·도움 인계·권한 준수·불필요한 대기/충돌 같은 관찰값으로 정의한다. JEV confidence나 자기평가를 정답 보상으로 사용하지 않는다.
- 기억 제공 여부, 고정 규칙, JEV 판단, 학습 정책을 분리 비교한다. 외부 기억 추가로 행동이 변한 것을 JEV weight 학습이라 부르지 않는다.
- train/eval seed·geometry·사건 조합을 분리하고 새로운 조합에서 baseline 대비 완료·위반·교착·관측 누출·지연을 비교한다.
- policy는 실험 결과물이다. 검증 전 live Player에 교체하거나 플레이 중 자동 weight update하지 않는다. trainer/PyTorch는 기본 Player 배포 제외다.

### 9.2 다른 두 도구를 보류하는 구체 이유

ML-Agents는 Unity sensor/action·고빈도 물리 제어를 직접 학습할 때 유용하다. 현재 우선 문제는 외부 worker/의미 행동 실험이므로 Unity package·Python communicator·inference backend를 추가할 필요가 없다.
읽은 공식 `main`은 Unity package `3.0.0-exp.1`, Python `3.10.1..3.10.12`, NumPy `<2`를 선언하여 현재 physics CPython3.12/NumPy2 환경과 다르다.[P4]
이 가변 `main`이나 deprecated 설치 문서를 최신 지원판 전체의 판정으로 확대하지 않는다. 채택 재검토 시 대응되는 release의 Unity/Python 쌍부터 다시 고정한다.
PettingZoo는 여러 학습 주체가 공동 전이/개별 보상을 갖는 실험에 맞지만, API를 추가하는 것만으로 SB3가 MARL learner가 되지는 않는다.[P5]
따라서 첫 실험에는 설치하지 않고 실제 공동학습 가설이 생길 때 Parallel/AEC 선택·동기 step 의미·학습기 호환을 정한다. 공식 README는 Windows를 공식 지원하지 않는다고 밝힌다.

## 10. SQLite·JSON: 이미 있는 저장/직렬화 재사용

- 저장 기반은 `Assets/ChooGuard/Persistence/SqliteProvider.cs`, `SqliteRunStore.cs`를 재사용한다. 두 번째 SQLite wrapper·별도 memory DB·vector DB를 먼저 도입하지 않는다.
- 기억에는 actor/run, 관측 시점, 출처, 알려짐/미확인, 사건/행동 결과 참조, revision을 보존한다. 발화 기억과 세계 사실은 같은 row로 합치지 않는다.
- world writer가 승인한 결과를 저장 계층에 넘긴다. JEV/대화/learner가 같은 SQLite 파일을 경쟁적으로 수정하지 않는다. 연구는 snapshot/export를 읽는다.
- 현재 SqliteProvider는 binary SHA/source ID를 확인하고 WAL·FULL·foreign_keys를 설정하지만 **macOS 이외에서 명시적으로 실패**한다.
- 코드의 최소 SQLite version number `3051003`은 3.51.3 하한이지 현재 설치 DLL 판본 확인이 아니다. Windows x64 DLL 로더·호출 ABI·배포 binary pin이 필요하다.
- 동일 Windows Player에서 저장→정상 재개, 프로세스 중단 후 복구, schema migration, 동시 읽기, disk-full을 실행해 확인해야 한다. macOS probe로 갈음하지 않는다.
- 기존 고정 DTO의 JsonUtility 사용은 유지하되 새 NPC/미래 wire에는 필요한 필드의 존재/null·추가 필드·숫자 유효성을 엄격히 구분한다.
- 새 경계의 엄격한 parsing은 이미 해결된 Newtonsoft.Json UPM을 활용한다. 직접 의존하게 되면 구현 단계에서 UPM 직접 의존성을 명시하고 중복 NuGet DLL을 설치하지 않는다.
- JsonUtility는 누락된 중첩 객체를 기본 DTO로 만들 수 있다고 `MvpPhysicsBridge.cs`에도 명시돼 있다. 0·false·빈 DTO를 unknown과 동일시하지 않는다.
- JSON Schema는 형식 검증용이다. 최신 revision, 관측 접근권, 후보 소속, reservation/consent, 실제 완료 증거는 runtime 의미 검증이 필요하다.
- cross-language hash는 임의 JSON 재직렬화 결과를 비교하지 않는다. 상위 wire 계약의 canonical/원문 hash 소유권을 따르고 worker가 요청 대응값을 붙인다.

## 11. Windows 배포와 과학 모델의 적용 한계

### 11.1 배포안

기본 Player는 Windows x64/Mono로 유지한다. 외부 worker는 별도 프로세스이며 Python을 Unity 내부에 임베딩하지 않는다.
Windows scientific profile은 **고정된 CPython3.12 계열 x64 런타임 + 검증된 wheel/site-packages + worker/원본 모듈/자료 파일을 담은 전용 디렉터리**로 배포하는 안을 우선한다.
실제 patch version·wheel·native DLL의 Windows 조합은 미검증이므로 macOS `3.12.11` 환경 자체를 shipping artifact로 선언하지 않는다.

1. 현재 `MvpPhysicsBridge.Launch`의 `.venv/bin/python`과 repo-relative `.tools` 경로를 플랫폼별 배포 manifest의 실행파일/자료 경로로 cutover한다.
2. Windows에서 CPython/JuPedSim/NumPy/Shapely(foreign native libraries)/fdsreader와 선택한 pyFDS 모듈을 함께 import·계산하고 정확한 의존성/해시를 고정한다.
3. `--no-deps` 설치는 호환 검증이 아니다. JuPedSim1.4.2 metadata의 `deprecated~=1.2.18`과 현재 pin `1.3.1`은 범위가 다르므로 실제 closure를 조정·검증한다.
4. GUI용 PySide/VTK 제외가 사용 모듈에서 유지되는지 확인한다. 생략한 optional 기능을 “전체 JuPedSim 설치”로 표시하지 않는다.
5. DLL 로드 경로, VC runtime 필요 여부, DLL bitness, 공백/한글 경로, 일반 사용자 권한, UTF-8 stdin/stdout, 종료/취소 후 자식 프로세스 회수를 실제 Windows에서 확인한다.
6. 사용자 PC에서 pip/컴파일러/download를 실행하지 않는다. Python 런타임·package notices·native 라이브러리·원본 변경사항을 함께 배포 기록한다.
7. 로컬 Node proxy를 배포하면 Node runtime도 별도 고정 artifact로 검증한다. 개발 host의 Node 설치나 셸 PATH를 제품 필수조건으로 숨기지 않는다.
8. 학습용 Torch, Scenic 저작 도구, FDS batch solver는 기본 Player에 넣지 않는다. 사전 계산 자료를 읽는 것과 solver를 배포하는 것을 구분한다.

### 11.2 지원 범위를 넘는 계산을 만들지 않기

현 `workers/physics/worker.py`는 30×20×4m 단층 기준 공간, dt0.05s, 최대200명, FDS0–120s, 1.5m 높이 단면의 native-node sampling을 명시한다.
이는 새 부산역 다층 geometry·수백 NPC 세계·임의 사건을 검증한 모델이 아니다. 별도 1,800초 case 파일의 존재만으로 현재 worker 범위가 확장되지 않는다.
FDS→JuPedSim 단방향이므로 문·환기·소화의 변경이 화재장을 실시간 다시 계산하는 효과를 주장할 수 없다.
풍수해·지진·보안 사건은 각 사건의 근거 있는 상태 전이로 다루고 FDS/PhysX를 임의의 침수·구조파괴·임상 생존 솔버로 확장 해석하지 않는다.
`pressureIndicator`는 Pa가 아니며, FED와 사람의 생존/치료·압착력은 다르다. 범위 초과는 unsupported/incomplete로 표시하고 마지막 값 연장이나 가짜 수치로 숨기지 않는다.
수백 NPC로 확장할 때는 최대200명 제한, 64KiB inline 결과 한도, `result()`의 이웃 전수 검색 비용을 함께 해결한다. 인원 숫자만 올리는 변경은 충분하지 않다.

## 12. 검증 도구와 실제로 확인할 관찰

아래는 **후속 실행 방법**이다. 이 문서 작성에서는 실행하지 않았고 parent가 설계 산출물 통합 후 필요한 검사를 수행한다.

| 대상 | 사용할 기존 도구·접점 | 통과를 보여야 하는 관찰 |
|---|---|---|
| 설계/wire | JSON Schema Draft202012Validator, 지역 reference registry | 정상 예제 수락; 누락/알 수 없는 후보/낡은 응답 등은 형식·의미 책임을 나눠 거부 |
| v5 상태 | `docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py`의 `validate`, `render --check` | active v5 참조 일관성; 이 도구가 새 NPC schema나 게임을 검증한다고 주장하지 않음 |
| 상세 조작 | Test Framework PlayMode + 실제 FPS Player 조작 | 막힌 문·잘못된 공구·점유 중 물체·취소 후 재접근에서 세계 사실/완료가 올바름 |
| 기존 절차 | `Assets/ChooGuard/Tests/PlayMode/FpsProcedureTests.cs`, FpsPromptPipeline/FpsAuditTerminal tests | 관측 전 완료 거부 등 소비자 행동 보존; test count를 품질 근거로 쓰지 않음 |
| 이동 | Unity Scene gizmo/Physics Debugger + fixed seed 상황 실행 | 층간·닫힌 문·양방향 병목·정지 작업점, 인원 보존·중복 writer 없음 |
| 성능 | Unity Profiler CPU Timeline/Physics/Rendering/Memory, ProfilerRecorder, 기존 Performance Testing | 목표 PC에서 수백 actor와 직접 조작 동시 실행; main-thread/frame p95·p99, GC, worker RTT/queue, 원격 결정 적용 지연 |
| 과학 worker | `workers/physics/reproduce.py`, `hazard_field_probe.py`, `verification/run_probes.py` | 기존 기준 case 범위 재현·범위 밖 거부; 실역 validation과 분리 |
| Windows | `ChooGuard.Editor.Bootstrap.BuildBaseline.Build`, 실제 Windows 개발 Player | Mono x64 로드·입력·저장·worker·종료 관찰; 빌드 성공만으로 완료하지 않음 |
| 외부 판단 | 고정 한국어/부분관측/지연된 답 사례, 실제 endpoint 후속 실험 | provider별 정확한 모델/usage·deadline·invalid/stale 거부, 금지된 사실/행동 유입 없음 |
| 학습 | Gymnasium/SB3 환경 점검 + 고정 seed baseline 비교 | API 적합성 외에 실제 전이/완료 증거·holdout 개선·권한 위반·실패 사례 확인 |

로컬 읽기 전용 preflight에서 확인한 도구는 `/opt/homebrew/opt/python@3.14/bin/python3.14`와 `jsonschema==4.26.0`이다.
모듈 경로는 `/Users/um-yunsang/Library/Python/3.14/lib/python/site-packages/jsonschema/__init__.py`이며 이 경로는 개발환경 관찰값이지 배포 설정이 아니다.
이 preflight는 module 위치/metadata 확인일 뿐 schema validation 실행이 아니다. 기존 `docs/CHOOGuard_Story_Plan_v4/requirements.txt`도 jsonschema4 계열을 선언한다.
현재 `BuildBaseline.Build`는 Windows target에 Windows host를 요구한다. macOS 실행으로 Windows 검증을 대신하거나 IL2CPP/다른 플랫폼으로 몰래 바꾸지 않는다.

## 13. 제거·cutover와 권리 메타데이터

- 새 NPC 경로가 활성화될 때 그 actor를 움직이던 기존 화면용 transform 갱신·타이머 완료·일괄 cohort 명령 연결은 해당 actor 경로에서 제거한다. 과학 비교 profile 자체는 유지한다.
- DotRecast를 유지하는 동안 Unity NavMeshAgent·다른 RVO solver를 병렬 기본 경로로 추가하지 않는다. 교체 시 caller·베이크·경로 오류 의미까지 한 번에 이관한다.
- `/turnaround`는 다른 기능이므로 유지한다. 도달 불가능한 forecast 분기는 proxy 수정 시 호출부 확인 후 삭제하고 신규 API의 fallback/alias로 남기지 않는다.
- 불필요한 XR·GOAP·Scenic·trainer 패키지가 Player에 포함되지 않게 배포 구성을 분리한다. 실험이 채택되지 않으면 실험용 의존성/adapter만 제거하고 연구 결과는 보존한다.
- SQLite의 macOS 경로를 Windows에서 조용히 다른 DB/in-memory 저장으로 대체하지 않는다. 동일 저장 계약의 Windows 구현으로 전환한다.

| 대상 | 확인한 권리 메타데이터 | 배포 시 남길 구분 |
|---|---|---|
| Input System, Unity Newtonsoft 포장 | 로컬 LICENSE: Unity Companion License | Unity 종속 포장과 upstream Newtonsoft MIT notices 구분 |
| NuGetForUnity | 로컬 LICENSE: MIT | NuGetForUnity 자체와 내려받은 package별 권리 별도 |
| DotRecast | 로컬 `LICENSE.txt`: zlib 형식 3조건 | 원저자/변경 표시·원문 고지 유지; 구현/geometry 권리는 별도 |
| JuPedSim / pyFDS-Evac | 선행조사·metadata LGPL-3.0-or-later / upstream lock MIT | 별도 프로세스라는 이유만으로 native 재배포 의무가 사라진다고 가정하지 않음 |
| Scenic / Gymnasium / SB3 | 선행조사 BSD-3-Clause / 공식 태그 MIT / MIT | native 하위 의존성·학습 데이터/모델 산출물 별도 |
| MscModApi | 선행조사 원문 MIT | My Summer Car 본체·게임 DLL·모델은 포함된 권리 아님 |
| OpenBVE / Open Rails | 파일별 permissive/public-domain 계열 / GPL-3.0-or-later | 노선·차량·음원, 실제 원용 파일별 조건 별도; 현 선택은 참고만 |
| XRI Examples / JEV·대화 서비스 | XRI 개별 LICENSE 추가 확인 필요 / 서비스 약관·데이터 경로 | SDK 공개와 모델 weights/재배포 허가는 다름; 유료/권리 때문에 연구 제외하지 않음 |

## 14. 근거 링크

선행자료는 전체를 재조사하지 않고 아래 상세 조사·원문 목록을 재사용했다.

- [FPS 기술·상호작용·철도 코드 조사](../../../CHOOGuard_FPS_Prior_Research_20260925/FPS_TECH_SOURCES.md)
- [NPC·Scenic·JuPedSim·GOAP 조사](../../../CHOOGuard_FPS_Prior_Research_20260925/NPC_RANDOM_SCENARIOS.md)
- [JEV 모델/API/SDK 조사](../../../CHOOGuard_FPS_Prior_Research_20260925/JEV_MODEL_SOURCES.md), [적합성 및 선행 실험](../../../CHOOGuard_FPS_Prior_Research_20260925/JEV_NPC_FEASIBILITY.md)
- [P1: SB3 v2.7.0 정확한 의존성](https://github.com/DLR-RM/stable-baselines3/blob/v2.7.0/setup.py)
- [P2: Gymnasium v1.2.0 정확한 의존성](https://github.com/Farama-Foundation/Gymnasium/blob/v1.2.0/pyproject.toml)
- [P3: Scenic v3.1.1 정확한 의존성](https://github.com/Scenic-Foundation/Scenic/blob/v3.1.1/pyproject.toml)
- [P4: ML-Agents main Unity package](https://github.com/Unity-Technologies/ml-agents/blob/main/com.unity.ml-agents/package.json), [Python 범위](https://github.com/Unity-Technologies/ml-agents/blob/main/ml-agents/setup.py) — 가변 branch 관찰, 최신 안정 지원판 보장 아님.
- [P5: PettingZoo 공식 README](https://github.com/Farama-Foundation/PettingZoo/blob/master/README.md) — MARL API 및 Windows 지원 경계.
- [P6: TypeSafe 공식 합성 지침](https://docs.typesafe.ai/concepts/how-to-build-with-system-one.md) — code-owned control flow·부작용, 독립 primitive를 코드로 합성; 내장 agent framework가 아님.
- [P7: Jev 1.13 공식 한계](https://docs.typesafe.ai/model-jaggedness/jev-1.13.md) — 자유 생성 비대상, 정밀 수치/시간 계산과 논리 불변식은 코드 책임.

**결론:** **FPS 기반 비상상황 대응 시뮬레이션 게임**을 위해 기존 FPS·DotRecast·PhysX·native UI·SQLite를 유지한다. 코드 소유 `NpcAgentRuntime`의 자발적 목표·계획·행동·재계획과 `FutureComposer`의 JEV 기반 동적 미래 합성을 핵심 런타임으로 설계하고, 실제 필요한 군중 회피·direct 판단/전이·별도 대화 경계를 결합한다. 완성 사건 pack 선택으로 이를 대체하지 않으며 GOAP/XRI/추가 agent framework를 자동 도입하지 않는다. Scenic은 선택적 초기 조건/holdout 도구이고, 외부 기억과 PPO 정책학습 연구는 별도 책임으로 유지한다. 현재의 Windows ABI·배포 경로·200명/메시지 한계와 실역 geometry 미검증을 먼저 드러내고 해결한다. 이는 설치나 런타임 성공 보고가 아니다.
