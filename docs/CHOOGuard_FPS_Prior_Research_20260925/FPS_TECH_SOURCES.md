# CHOOGuard FPS·상호작용·멀티플레이·물리 선행자료 조사

> **개발계획 아님 / 실행 미검증 / 최신 지시에 따라 라이선스를 검색·선정 제외 필터로 사용하지 않음.** 무료·상용·계정 필요 자료를 함께 조사했다. 사용 조건은 짧은 메타데이터이며, 도입 승인이나 법률 자문이 아니다. 기존 모델링·Assets·Packages·ProjectSettings·기존 설계 문서는 수정하지 않았다.

- 조사 기준일: **2026-09-25**.
- 사용자 제공 환경: Unity **6000.3.23f1**, URP **17.3.0**, Input System **1.20.0**. `FirstPersonResponder.cs`, `TutorialSession.cs`의 존재는 전달된 컨텍스트이며 이번 조사에서 구현을 재감사하지 않았다.
- `graphify-out/GRAPH_REPORT.md`의 2026-09-21 보고서 도입부를 확인했다. 과거 아키텍처는 참고 맥락이지 최신 승인으로 간주하지 않는다.
- 확인 수준: **원문**=README/공식 문서/소스/실제 LICENSE 본문 열람, **소개**=공식 제품 설명만 확인, **이미지 직접 확인**=이미지 파일을 열어 관찰, **영상 안내 확인·미재생**=공식 페이지가 링크한 영상의 제목·설명만 확인.
- 아래 후보의 “바로 재사용”은 **공개 코드에서 추출 검토할 구체 재료가 있다**는 뜻이다. 현 프로젝트에 곧장 임포트·컴파일·네트워크 동작한다는 뜻이 아니다. 설치·클론·다운로드 패키지 실행·빌드·테스트·성능 측정은 하지 않았다.
- 별도 날짜가 없는 README·문서는 **발행/최종 갱신일 미표기, 기준일 열람**이다. 저작권 연도와 릴리스 날짜를 혼동하지 않았다.

## 1. 핵심 발견

1. **총격전 전체 템플릿보다 문·잡기·부품 상태를 가진 작은 샘플이 더 직접적인 재료다.** Boss Room의 `SwitchedDoor`, `PickUpAction`, XRI의 physics door/key/socket/drawer, MscModApi의 `Part`/`Screw`가 실제로 확인됐다. 단, Boss Room 문은 물리적으로 문짝을 미는 구현이 아니라 열림 상태에 따라 충돌 오브젝트를 끄는 예시다.
2. **“Unity Character Controller”는 세 계열을 분리해서 봐야 한다.** 기존 GameObject `CharacterController`, Asset Store의 Starter Assets/새 First+Third Person 상품, DOTS `com.unity.charactercontroller`의 Standard Characters는 같은 제품이 아니다. DOTS OnlineFPS를 기존 MonoBehaviour 이동 코드에 그대로 붙이는 판단은 잘못이다.
3. **NGO의 anticipation, Entities의 prediction, 게임 복기 replay는 서로 다른 개념이다.** 확인한 NGO 2.7 문서는 완전한 rollback-and-replay prediction loop를 제공하지 않는다고 명시한다. FishNet에는 replicate/reconcile 경로와 `PredictionRigidbody` 소스가 있다. 어느 쪽도 그것만으로 시민의 의사결정·업무 판단 기록까지 복원하지는 않는다.
4. **FPS Sample은 이름 때문에 우선 선택하면 안 된다.** 공식 저장소가 Unity 2018.3 기반·유지보수 중단·HDRP라고 명시한다. 반대로 Boss Room README는 6000.0.52f1/NGO 2.4.3, XRI Examples는 6000.3/XRI 3.4.0을 명시한다. 이 차이는 단순 릴리스 숫자가 아니라 마이그레이션 범위의 차이다.
5. **현실감과 검증된 물리 정확도는 별개다.** PhysX/Jolt/Unity Physics는 충돌·관절·상호작용, Obi는 천·호스·입자 유체, AGX는 산업 기계 모델링에 좋은 재료다. 연기·열·유독성·피난 안전을 입자 효과의 예쁜 모양으로 판정해서는 안 된다. NIST FDS는 별도의 저속 유동 LES/화재 연기·열 해석 계열이다.

## 2. Unity 유지와 Unreal/Godot 전환의 연구상 비교

아래는 **[연구판단]**이며 교체 결정이나 개발 일정이 아니다.

| 관점 | Unity 유지 | Unreal 전환 | Godot 전환 |
|---|---|---|---|
| 이미 가진 투자 | 현재 C#·URP·Input System 및 진행 중인 모델링을 유지할 가능성이 가장 높다. 실제 결합도는 이번 조사 범위 밖 | Unity 컴포넌트·씬 동작·입력·셰이더를 UE Actor/Component, C++/Blueprint, Enhanced Input 체계로 다시 연결해야 한다 | 씬/노드·리소스·입력·물리 표현을 다시 연결해야 한다. C# 문법을 쓸 수 있다는 것과 Unity API 이식 가능성은 다르다 |
| 확인된 장점 | 작은 NGO/XRI/Physics 샘플, 물리·네트워크 선택지가 풍부 | Lyra의 모듈형 Experience·상호작용 옵션·GAS·온라인 흐름, 엔진의 replication 기반 Replay System | Jolt 통합 소스와 관절 예제가 공개돼 내부 관찰·수정 가능. 통합 확장에는 MIT LICENSE 원문 존재 |
| 확인된 부담 | GameObject/PhysX와 ECS/Unity Physics의 물리 세계·데이터 경로를 혼동하기 쉽다 | Lyra는 곧바로 철도 FPS가 아니다. 샘플 의존성과 엔진 버전 갱신을 따라야 하고, 공식 GitHub는 Epic 계정 연결 필요 | 외부 godot-jolt 확장은 maintenance mode. 내장 모듈과 joint 인터페이스 차이가 있고 확장판의 결정론 보장은 없다 |
| 정비·물리 | 작은 문/공구 상호작용은 기존 Rigidbody·joint 기반 참고가 가까움. 정밀 기계는 ArticulationBody/AGX 비교 가능 | 이번에 확인한 Lyra 문서는 대상 검색·옵션·행동 시스템에 강점. 산업 역학 정확도가 자동으로 높아진다는 증거는 없음 | Jolt joints 실험에는 좋지만 작은 체결부·접촉력·네트워크 결정론을 별도 검증해야 함 |
| 복기 | 네트워크 샘플만으로 업무 판정 이력을 얻는 것은 아님 | `DemoNetDriver`가 replication 데이터를 기록·재생하는 공식 기반 존재 | Jolt 자체/통합의 결정론 차이를 고려해야 하며 모든 상태를 seed만으로 복구 가능하다고 보면 안 됨 |
| 현재 근거가 지지하는 판단 | **당장 엔진을 바꿀 만한 필수 결함은 이번 공개자료에서 확인되지 않음** | UE 전용 작업 방식·팀 역량·Replay 등 구체 필요가 Unity 투자보다 중요할 때 비교 대상 | 소스 통제·가벼운 별도 물리 연구가 우선일 때 비교 대상. 엔진 교체 자체가 게임 문제의 해답이라는 근거는 없음 |

근거: [Unity joints](https://docs.unity3d.com/6000.0/Documentation/Manual/Joints.html), [Unity articulations](https://docs.unity3d.com/6000.0/Documentation/Manual/physics-articulations.html), [Lyra 공식 소개](https://dev.epicgames.com/documentation/unreal-engine/lyra-sample-game-in-unreal-engine?lang=en-US), [UE 5.6 Replay](https://dev.epicgames.com/documentation/unreal-engine/using-the-replay-system-in-unreal-engine?application_version=5.6), [Godot Jolt 저장소](https://github.com/godot-jolt/godot-jolt).

**이식 자산 메모:** 일반 모델 원본이 있다고 해서 머티리얼·리깅·물리 설정·셰이더·스크립트까지 자동 이식되지는 않는다. Unity Companion/UE-Only 샘플은 범용 소스와 조건이 다르므로 검색에는 포함하되, 다른 엔진에 복사할 수 있다고 전제하지 않았다. Unreal EULA 본문 요청은 403이었고, 가격·로열티를 확정하지 않았다.

## 3. 구체 후보 12개 + 정비 상태 모델 보조 후보

### 비교 요약

| # | 후보·정확한 공식 경로 | 분류 | CHOOGuard에 유용한 위치 | 확인된 유지보수/호환 조건과 한계 |
|---|---|---|---|---|
| 1 | [Unity First Person + Third Person Character Controllers](https://assetstore.unity.com/packages/3d/characters/first-person-third-person-character-controllers-196526) | **바로 재사용 후보, 패키지 원문 미확보** | 기본 시점·이동 입력·URP 캐릭터 출발점 | 상품 본문: 2.0.1, **2026-09-17**, 6000.3.0f1 URP Compatible. 현 6000.3.23f1/URP 17.3.0/Input 1.20.0 조합은 미검증. 원문 패키지의 파일/클래스는 추정하지 않음 |
| 2 | [Unity-Technologies/CharacterControllerSamples](https://github.com/Unity-Technologies/CharacterControllerSamples) | **참고만 우선 / ECS 채택 시 재사용 후보** | 경사·장애물·움직이는 바닥·1인칭 카메라·클라이언트 예측 | `Basic`, `StressTest`, `Platformer`, `OnlineFPS`가 공식 문서에 존재. DOTS·Unity Physics·Entities·Netcode for Entities 의존. 개별 샘플 엔진/패키지 lock은 미확보 |
| 3 | [Boss Room](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop), [Bitesize](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.bitesize) | **바로 재사용 후보** | 협동 세션, 접속/재접속, 문·스위치, 물체 잡기·던지기, 서버 상태 | Boss Room: 8인 교육 샘플, 6000.0.52f1, NGO 2.4.3, release 3.0.0 표기. Bitesize: 2022.3+/NGO 2.0+ 표기, 특정 Addressables 샘플은 NGO 1.7.1 잔류 경고. 전투 로직은 덜어내고 패턴을 읽을 대상 |
| 4 | [Megacity Metro](https://github.com/Unity-Technologies/megacity-metro), [ECS Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples) | **참고만 우선 / 대규모 ECS 필요 시 재사용 후보** | 서버 권한·prediction·interpolation·interest/대규모 상태 처리의 연구 | Megacity: Unity 6/URP/Entities Graphics, README 150인, 저장소 설명 128+로 수치도 다름. 실제 CHOOGuard 성능 근거 아님. ECS Samples README: Unity 6.2 + Entities/Netcode/Physics/Graphics 1.4 계열. GitHub 이슈/PR 미수용 안내 |
| 5 | [MirrorNetworking/Mirror](https://github.com/MirrorNetworking/Mirror) | **바로 재사용 후보** | GameObject 서버 권한, RPC·상태, 위치 보간·공간별 동기화 | README 지원 목록에 Unity 6000.1까지 명시. snapshot interpolation Stable, lag compensation Beta, prediction Researching 표기. “Mirror이면 완전 예측이 해결”로 해석 금지 |
| 6 | [FirstGearGames/FishNet](https://github.com/FirstGearGames/FishNet) | **바로 재사용 후보 / Pro는 별도 상용검토** | 물체/플레이어 prediction, replicate/reconcile, 전용 서버 | 공식 문서가 서버 권한 설계·임의 호스팅·CCU 제한 없음 설명. `PredictionRigidbody` 실제 C# 원문 확인. 정확한 6000.3.23f1 호환과 최신 릴리스 날짜는 이번에 미확보 |
| 7 | [Unity-Technologies/XR-Interaction-Toolkit-Examples](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples) | **바로 재사용 후보, 비XR는 입력·조작 재해석 필요** | 잡기, 키/소켓, 문손잡이, 서랍, 공구 트리거·3D UI | README XRI 3.4.0/Unity 6000.3 명시. VR 장치 입력을 데스크톱 FPS에 그대로 붙일 수는 없음. 문/서랍의 물리 모델 자체는 매우 가까운 자료 |
| 8 | [EpicGames/UnrealEngine → Samples/Games/Lyra](https://github.com/EpicGames/UnrealEngine/tree/ue5-main/Samples/Games/Lyra), [Fab Lyra](https://www.fab.com/listings/93faede1-4434-47c0-85f1-bf27c0820ad0?lang=en) | **Unity에서는 참고만 / UE 전환 시 별도 검토** | 상호작용 옵션 검색·행동 실행, 모듈형 세션/Experience, 장비·온라인 흐름 | Epic 공식 안내 **2023-02-09**: 계정 연결 후 소스 열람. 공식 문서의 클래스·경로 확인, 인증 GitHub 소스 자체는 미열람. Fab Content와 소스 획득 경로를 구분 |
| 9 | [godot-jolt/godot-jolt](https://github.com/godot-jolt/godot-jolt), [jrouwe/JoltPhysics](https://github.com/jrouwe/JoltPhysics) | **참고만 / Godot 전환·별도 물리 연구 후보** | 관절·강체·캐릭터 충돌, 확장 가능한 소스 | Godot 4.4부터 내장 통합. 외부 확장 README 지원 4.3–4.6·maintenance mode. Jolt 코어의 기능과 Godot 통합 지원은 동일하지 않음 |
| 10 | [Obi 공식 사이트](https://obi.virtualmethodstudio.com/) → Cloth/Rope/Fluid/Softbody Asset Store | **별도 상용검토** | 호스·밧줄·방수포·천·유출 액체의 상호작용 | CPU/GPU 입자 프레임워크 소개 및 7.0 `ObiSolver` 매뉴얼 본문 확인. 구매 패키지·Unity 6000.3/URP 17.3 호환 행렬·소스 범위는 미확보. 화재/유독성 검증 솔버로 보지 않음 |
| 11 | [Algoryx/AGXUnity](https://github.com/Algoryx/AGXUnity), [Algoryx/AGXUnityScenes](https://github.com/Algoryx/AGXUnityScenes) | **별도 상용검토** | 산업 기계 joints, cable/wire, 마찰·하중, 로봇·작업 장비 | 바인딩은 Apache-2.0, AGX Dynamics 실행 라이선스 별도. README 표: AGXUnity 5.5–Unity 2022.3–AGX 2.41.1.0. 공식 번들 Windows 64-bit 표기; 현 macOS arm64 작업환경·Unity 6.3 대응은 미확보 |
| 12 | [Unity-Technologies/FPSSample](https://github.com/Unity-Technologies/FPSSample) | **신규 기반으로 비권장 / 역사적 참고만** | 과거 권한·클라이언트/서버 구분, FPS 제작 툴 관찰 | 공식적으로 유지보수 중단. Unity 2018.3.8f1, HDRP, Windows 클라이언트·Linux 서버, Assets 약 18GB와 Git LFS 요구. 현 URP 프로젝트의 빠른 출발점이 아님 |
| 보조 | [MarvinBeym/MscModApi](https://github.com/MarvinBeym/MscModApi) | **상태 모델 참고만** | 부품 설치/체결 구분, 공구 규격, 저장·부모 의존·이벤트 | 공개 커뮤니티 모드 API이지 My Summer Car 원본 소스가 아님. 게임 DLL/ModLoader에 결합돼 독립 Unity SDK로 바로 가져오기 어렵다 |

### 후보 1과 2: 이름이 비슷한 컨트롤러를 분리할 것

- 구 [Starter Assets FirstPerson 상품 196525](https://assetstore.unity.com/packages/essentials/starter-assets-first-person-character-controller-196525)은 열람 시 **deprecated, 신규 구매 불가·지원 중단** 안내로 이동했다. 이미 보유한 계정의 My Assets 이용 안내는 남아 있다.
- 새 [First Person + Third Person 상품 196526](https://assetstore.unity.com/packages/3d/characters/first-person-third-person-character-controllers-196526)은 실제 목록이 살아 있고 무료, Non standard EULA, 2026-09-17 릴리스다. **이 상품과 DOTS package를 동일시하지 않는다.** 상품 패키지 원문을 받지 않았으므로 `FirstPersonController.cs`가 그 최신판에 있다고 단정하지 않았다.
- DOTS [Standard Characters 네트워킹 가이드](https://github.com/Unity-Technologies/CharacterControllerSamples/blob/master/_Documentation/Tutorial/tutorial-netcodecharacters.md)는 패키지의 Samples 탭에서 `Standard Characters`를 가져와 `Assets/Samples/Character Controller/[version]/Standard Characters`로 들어간다고 명시한다. `com.unity.charactercontroller`, `com.unity.netcode`, `com.unity.entities.graphics`와 Entities/Physics 의존성도 명시한다.
- [OnlineFPS Character and Camera](https://github.com/Unity-Technologies/CharacterControllerSamples/blob/master/_Documentation/Samples/OnlineFPSSample/character-and-camera.md)에서 확인한 실제 식별자: `GhostVariants`, `FirstPersonCharacterComponent.CharacterYDegrees`, `.ViewPitchDegrees`, `FirstPersonCharacterAspect.VariableUpdate`, `BuildCharacterRotationSystem`. 샘플 상대 경로 `Assets/Prefabs/Ghost`도 문서 확인. 이는 **클래스/경로가 문서에서 확인된 수준**이며 각각의 C# 파일 전체를 검증했다는 뜻은 아니다.
- 유용한 원리: 캐릭터 yaw와 시선 pitch를 나누어 동기화하고 rollback/resimulation에서도 회전을 재구성한다. 이동 플랫폼은 `ParentEntity`, `ParentLocalAnchorPoint`, `ParentVelocity` 동기화 항목이 가이드에 있다. 철도 현장 이동 표면을 연구할 때 관련성이 있지만, 실제 열차 역학/충돌 검증의 대체물은 아니다.
- 문서의 `@latest` 링크는 이번 조회에서 `@6.7`로 이동한 뒤 유의미한 매뉴얼 본문 없이 Home Page만 보였다. **이 숫자를 설치 가능 안정 버전 또는 본 샘플 요구 버전으로 채택하지 않았다.**

### 후보 3: 총 대신 문·스위치·잡기·세션 코드를 볼 곳

Boss Room README의 공식 [리소스 인덱스](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop#readme)에서 아래 경로가 확인된다.

| 경로/식별자 | 확인 수준 | 가져올 원리와 적용 한계 |
|---|---|---|
| `Assets/Scripts/Gameplay/GameplayObjects/SwitchedDoor.cs` | **[실제 C# 원문](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Assets/Scripts/Gameplay/GameplayObjects/SwitchedDoor.cs)** | `NetworkVariable<bool> IsOpen`, 서버에서 switch 상태 결합, 클라이언트 시각/충돌 갱신. **문짝 연속 충돌/손잡이 토크가 아니라 충돌 오브젝트 활성/비활성 예시** |
| `Assets/Scripts/Gameplay/Action/ConcreteActions/PickUpAction.cs` | **[실제 C# 원문](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Assets/Scripts/Gameplay/Action/ConcreteActions/PickUpAction.cs)** | raycast 선택, 이미 다른 NetworkObject에 붙은 물체 거부, `TrySetParent`, 손 소켓 PositionConstraint. 물체 중복 점유와 서버 상태의 출발 자료. 두 사람이 긴 물체를 함께 드는 물리는 아님 |
| `Assets/Scripts/Gameplay/Action/ConcreteActions/TossAction.cs` | 공식 인덱스 | `NetworkRigidbody`를 이용한 던지기. 원문 미열람 |
| `Assets/Scripts/Gameplay/GameplayObjects/FloorSwitch.cs` | 공식 인덱스 | 정적 현장 장치의 state tracking. 원문 미열람 |
| `Assets/Scripts/Gameplay/GameplayObjects/Character/ServerCharacterMovement.cs` | 공식 인덱스 | 서버 측 이동 경로. Boss Room은 click-to-move이므로 FPS 이동과 동일하지 않음 |
| `Assets/Scripts/ConnectionManagement/ConnectionManager.cs`, `ConnectionState/HostingState.cs` | 공식 인덱스 | 접속 상태머신·승인/거절 이유·종료 처리 |
| `Packages/com.unity.multiplayer.samples.coop/Utilities/Net/SessionManager.cs`, `Utilities/SceneManagement/` | 공식 인덱스 | 재접속 세션과 공유 씬 로딩; 제품별 UGS 설정과 게임 상태 보존은 별도 |

Bitesize의 확인된 프로젝트 경로는 `Basic/MultiplayerUseCases`, `Basic/ClientDriven`, `Basic/DistributedAuthoritySocialHub`, `Basic/DynamicAddressablesNetworkPrefabs`다. ClientDriven는 networked physics/parenting 참고이지만, **클라이언트가 최종 안전 판정을 정하는 근거가 아니다.** Distributed Authority가 공유 세계라는 이유만으로 철도 상황 상태에 더 적합하다고 판단하지 않았다.

### 후보 4–6: 네트워크 선택에서 실제로 다른 점

| 항목 | NGO/Boss Room | Netcode for Entities/OnlineFPS/Megacity | Mirror | FishNet |
|---|---|---|---|---|
| 본문에서 확인한 표현 | 서버 권한 + latency masking/anticipation | 서버 권한 + client prediction + interpolation/lag compensation | 서버/클라이언트 권한 선택, snapshot interpolation | 서버 권한 + replicate/reconcile |
| 기존 MonoBehaviour와 거리 | 상대적으로 가까움 | ECS 데이터·baking·world/system으로 재구성 부담 | 상대적으로 가까움 | 상대적으로 가까움 |
| 실제 주의점 | **NGO 2.7 문서는 full prediction 없음 명시** | prediction 대상/물리 tick/replay 중 side effect를 구분해야 함 | README prediction은 Researching; Beta 항목을 안정 기능으로 포장하지 않음 | 힘·속도 변경을 PredictionRigidbody 경로로 통일하라는 문서 요구. 외부 스크립트 직접 조작과 충돌 가능 |
| CHOOGuard 관련성 [연구판단] | 소수 협동 인원·업무 상태·장치 소유권 연구의 첫 비교군 | 실제로 많은 시민/플레이어를 ECS로 운영할 필요가 밝혀질 때 가치 증가 | GameObject 기반 대안·interest management가 필요한 경우 | FPS 이동·강체 상호작용의 응답성과 보정이 중요한 경우 |

- [NGO 2.7 anticipation 문서](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects%402.7/manual/advanced-topics/client-anticipation.html): 생성 메타데이터 **2025-10-31**, `AnticipatedNetworkVariable<T>`, `AnticipatedNetworkTransform`, `OnReanticipate` 설명. 완전한 rollback/replay는 사용자가 작성해야 한다. **이 확인을 NGO 모든 미래 버전에 일반화하지 않는다.**
- [Megacity script index](https://github.com/Unity-Technologies/megacity-metro/blob/master/Documentation/script-index.md): `Assets/Scripts/Gameplay/Mix/Netcode/NetcodeBootstrap.cs`, `Server/Netcode/ServerInGame.cs`, `Client/Player/PlayerVehicleInputSystem.cs`, `Assets/Scripts/Utils/NetcodeExtensions/UI/NetcodePanelStats.cs` 등. 이동은 비행 차량 중심이라 도보 정비 상호작용의 직접 대체재는 아니다.
- Mirror 저장소 tree에서 `Assets/Mirror/Components/InterestManagement/Distance/DistanceInterestManagement.cs`, `Scene/SceneInterestManagement.cs`, `SpatialHashing/SpatialHashing3DInterestManagement.cs` 확인. 역사/규모에 관한 README의 홍보 수치는 독립 검증하지 않았다.
- FishNet [실제 파일](https://github.com/FirstGearGames/FishNet/blob/main/Assets/FishNet/Runtime/Object/Prediction/PredictionRigidbody.cs): `Assets/FishNet/Runtime/Object/Prediction/PredictionRigidbody.cs`, `FishNet.Object.Prediction.PredictionRigidbody`, 힘/토크/이동 데이터를 직렬화하는 코드 확인.
- FishNet [공식 예측 가이드 원문](https://github.com/FirstGearGames/FishNet-Documentation/blob/main/guides/features/prediction/creating-code/controlling-an-object.md): `IReplicateData`, `IReconcileData`, `[Replicate]`, `[Reconcile]`, `OnTick`/`OnPostTick`, `PredictionRigidbody.Simulate`/`.Reconcile`. 예시 입력은 기존 Input API를 사용하므로 현 Input System 1.20.0과 입력 수집을 그대로 같게 보지 않는다.

**서버 권한과 복기의 연구 원칙 [INFERENCE]:** 플레이어의 손/카메라는 빠르게 반응시켜도, 부품 체결 완료·열차/장치 상태 전환·시민 인계는 최종 승인 상태와 시각 예측을 구분할 필요가 있다. `RPC를 보냈음`과 `업무가 성공했음`은 다르다. 또한 prediction의 짧은 rollback은 훈련 세션 전체 복기와 다르다.

### 후보 7–9: 잡기와 문, 모듈형 상호작용, 엔진 대안

**XRI** [Physics Interactables 원문](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/PhysicsInteractables.md)은 강체를 강제로 순간 이동시키는 잡기와 물리 joint가 충돌하면 큰 힘/불안정성이 생긴다고 설명한다. Basic은 `XR Grab Interactable`의 Velocity Tracking, Advanced는 transform을 따라가되 막혔을 때 힘을 내는 **Transform Joint**를 설명한다. 문·키·socket·`XR Knob`·손잡이·축 제한 서랍이 구체 예다. 확인된 프로젝트 경로는 `Assets/XRI_Examples/Scenes/XRI_Examples_Main`, `Assets/XRI_Examples`이며 최신 클래스 파일 경로는 추정하지 않았다. Desktop FPS에는 마우스/커서 목표점·접근 거리로 번역할 수 있다는 것은 **[연구판단]**이다.

**Lyra**의 [UE 5.6 interaction 문서](https://dev.epicgames.com/documentation/unreal-engine/lyra-sample-game-interaction-system-in-unreal-engine?application_version=5.6)에서 `ULyraGameplayAbility_Interact`, `IInteractableTarget::GatherInteractionOptions`, `FInteractionQuery`, `FInteractionOption`, `UAbilityTask_WaitForInteractableTargets`, `AbilityTask_WaitForInteractableTargets_SingleLineTrace`를 확인했다. “대상 검색 → 가능한 옵션 → 실행자/대상의 행동” 분리는 공구·문·장비에서 유용한 개념이다. UE의 GAS 전체를 C#에 옮기라는 의미는 아니다. 공식 소개는 `LyraExperienceDefinition`, `ShooterCore`, `ShooterMaps`, `/ShooterCore/Maps/L_ShooterGym`을 설명한다.

**Godot/Jolt** 저장소 tree에는 `examples/scenes/joints/entities/hinge/hinge.tscn`, `slider/slider.tscn`, `six_dof/six_dof.tscn`, `rail/rail.tscn`이 실제로 있다. 여기의 `rail`이라는 이름을 철도 운행 시뮬레이터로 오해하지 않는다. 확장 README는 Godot Jolt 통합의 결정론을 보장하지 않는다고 명시한다. 확장판과 내장 모듈의 문서는 다르다.

- [Godot 4.4 문서](https://docs.godotengine.org/en/4.4/tutorials/physics/using_jolt_physics.html)는 당시 내장 Jolt를 experimental로 설명한다.
- [stable 문서](https://docs.godotengine.org/en/stable/tutorials/physics/using_jolt_physics.html)는 이번 조회에서 4.7 표제로 반환되며 새 프로젝트 기본 Jolt·일부 joint 차이를 설명했다. **stable URL은 가변이며 출시일은 이 본문에서 확인하지 못했으므로, “현재 최신 정식판이 4.7”이라는 결론은 내리지 않는다.** 과거 4.4의 모든 제한을 현재판에 그대로 씌우지도 않았다.

## 4. 물리 계층: 무엇을 사실적으로 만들 수 있고 무엇은 별개인가

| 물리 계열 | 확인한 적합 영역 | 필요한 분리·한계 |
|---|---|---|
| Unity 기본 PhysX / Rigidbody joints | HingeJoint는 문·힌지, ConfigurableJoint는 축/회전 자유도 제한, FixedJoint는 연결/파손, 힘/토크 | 완전한 기계 공학 검증이 아님. 작은 볼트의 나사산을 모두 실제 접촉으로 풀어야만 정비 절차가 성립하는 것은 아님 [연구판단] |
| Unity ArticulationBody | 로봇팔/연속 기구 체인을 위해 reduced-coordinate를 사용. prismatic/revolute/spherical/fixed | 공식 문서상 tree에 단일 root, **kinematic loop 불가**. 닫힌 고리 기구를 무조건 articulation으로 바꾸면 안 됨 |
| Unity Physics | ECS 강체·충돌·관절, 코드/샘플 수준 관찰 | MonoBehaviour PhysX의 교체 스위치가 아님. ECS 데이터 경로와 동기화 비용을 포함해야 함 |
| Havok Physics for Unity | Unity Physics와 같은 입력/출력 데이터로 ECS backend 전환 가능, stateful caching·stacking | [1.4.2 공식 문서](https://docs.unity3d.com/Packages/com.havok.physics@1.4/manual/index.html), 생성일 **2025-12-09**: closed-source/binary backend, 동작이 Unity Physics와 같지 않아 재튜닝 가능성. Visual Debugger는 Windows 한정. 정확도 보증/Unity 전체 물리 교체와 다름 |
| Jolt / Godot Jolt | 강체·관절·캐릭터 상호작용. 소스 접근성 | core와 통합의 결정론·관절 지원 차이. 확장 README는 동적 물체 0.1–10m 권고를 인용하므로 미세 나사산 접촉에 적합하다고 단정 불가 |
| Obi | cloth/rope/softbody/fluid의 입자·constraint 기반 상호작용 | `ObiSolver`끼리는 독립되어 서로 충돌하지 않음. substeps↑는 비용↑, interpolation/async는 지연. solver 가시성에 따른 simulation 설정은 판정에 영향을 줄 수 있음 |
| AGX Dynamics | 산업 장비·케이블/와이어·마찰·하중·기계의 joint/constraint | 모델 재료값·형상·조건 검증과 상용 런타임 검토가 필요. 샘플의 안정된 모습은 실제 설비 정확도 증명 아님 |
| FDS / Smokeview | 화재의 저속 LES·연기와 열 수송, 결과 시각화 | 범용 FPS 강체 엔진이 아님. 실시간 NPC 경로 판정과 물리 결과 연결은 별도 문제. NIST 사이트가 FDS+Evac 지원 종료도 명시 |

직접 확인한 [ECS PhysicsSamples README](https://github.com/Unity-Technologies/EntityComponentSystemSamples/blob/master/PhysicsSamples/README.md)에는 `4a. Joints Parade.unity`, `4b. Limit DOF.unity`, `4c2. Position Motor.unity`, `4c4. Angular Velocity Motor.unity`, `5e. Kinematic Motion.unity`, `6a. Character Controller.unity`, mouse spring drag와 query gizmo가 설명되어 있다. **이름은 README에서 확인한 scene 이름이며, 전체 `.unity` 위치를 추정하지 않았다.**

[AGXUnity](https://github.com/Algoryx/AGXUnity) tree에서 `AGXUnity/Cable.cs`, `CableAttachment.cs`, `CableDamage.cs`, `Constraints/Constraint.cs`, `Constraints/FrictionController.cs`, `Constraints/Generic1DOFControlledConstraint.cs`가 확인된다. [AGXUnityScenes](https://github.com/Algoryx/AGXUnityScenes)에는 `CableVSWire/Scripts/CraneControl.cs`, `CableRobot`, `ChargingStation`, `Forklift` 설명과 CAD→crane tutorial이 있다. 특히 **호스/케이블의 lumped-element 모델과 큰 하중용 wire 모델을 구분**하는 본문이 유용하다.

**화재/유체 accuracy 경계:** Obi Fluid는 표면장력·부착·와도·거품·액적 등 실시간 유체 기능이 확인된다. 그러나 해당 소개와 매뉴얼에서 철도 화재의 온도장·유독가스 농도·연기 광학밀도에 대한 검증 보고서는 확보하지 못했다. “물처럼 보임”과 “안전 판단에 쓸 수 있음”을 분리해야 한다. [NIST FDS 공식 본문](https://pages.nist.gov/fds-smv/)은 목적을 저속 유동·연기/열 전달로 명확히 한정하고 검증 보고서 경로를 제공한다.

## 5. My Summer Car에서 가져올 것은 자산보다 작업 모델

### 공개 확보 상태

- [Amistech 공식 소개](https://www.amistech.com/msc/) 본문 확인. 뉴스의 마지막 명시 항목은 **2024-12-31**의 Early Access 종료 준비, 하단 저작권 표시는 2016–2025. 이 둘은 게임 최신 릴리스 증거와 같지 않다.
- 공식 소개에는 부품 탈락·마모, 부품점, 차 검사, 점화플러그부터 차체까지 조립, oil/coolant/brake fluid/fuel/carburetor 관리, 1인칭 조작 등이 명시돼 있다.
- **원본 게임 전체 소스의 공식 공개 배포는 이번 조사에서 확보하지 못했다.** 검색에서 발견한 모드 코드와 디컴파일/재업로드를 공식 원본 소스와 혼동하지 않는다.
- 커뮤니티 [MscModApi](https://github.com/MarvinBeym/MscModApi)는 코드·문서·MIT 원문이 공개돼 있다. 요구 사항은 My Summer Car build 10201552/MSCModLoader 1.2.14로 표시된다. 기존 게임 DLL 참조와 모드 의존성이 있으므로 **CHOOGuard에 바로 임포트할 SDK가 아니다.**

### 실제 확인한 모델링 재료

| 확인 위치 | 실제 제공 내용 | CHOOGuard로의 적용 해석 [INFERENCE] |
|---|---|---|
| `Source code/MscModApi/Parts/Part.cs`, [Part API 문서](https://github.com/MarvinBeym/MscModApi/blob/master/docs/class-documentation/Parts/Part.md) | stable id, 표시 이름, 부모, 설치 위치/회전, `IsInstalled()`와 `IsFixed()` 구분, parent fixed, install block, pre/post install/fixed 이벤트 | “제자리에 놓음”과 “체결 완료”를 다르게 취급. 부모 부품의 상태가 자식의 작업 가능성을 바꿈 |
| `Source code/MscModApi/Parts/Screw.cs`, [Screw API 문서](https://github.com/MarvinBeym/MscModApi/blob/master/docs/class-documentation/Parts/Screw.md) | wrench size, 위치/회전, `In`/`Out`, 회전 단위 체결·해제 | 공구 규격과 작업 횟수의 UI/게임 모델 참고. 이 값을 실제 토크나 체결력으로 오해하지 않음 |
| `Source code/MscModApi/Tool.cs`, `Parts/PartSave.cs`, `Parts/EventSystem/` | repository tree에서 파일/폴더 확인; README는 설치·분리·볼트 상태 이벤트와 저장을 설명 | 공구·부품·검사 결과를 분리해 보는 재료. 각 파일 동작 전체를 확인했다는 의미는 아님 |
| 공식 게임 소개의 검사·유체·마모 | 조립 이외에도 정상 작동/유지 상태를 관리 | 정비 완료는 장착 애니메이션 종료와 다르며, 문서·검사·확인 결과가 다른 상태라는 연구 방향 |

**철도 교육으로 그대로 옮기지 말 것:** My Summer Car의 실수·영구사망·숨겨진 실패는 오락의 장치다. 철도 정비의 실제 분해 순서, 체결 토크, 자격/권한, 전원 격리, 검사 합격 기준은 장비별 공식 정비 문서에서 가져와야 한다. 여기서 제시한 구분은 **작업 UI/상태 표현 참고**이지 정비 절차나 안전 기준의 확정이 아니다.

## 6. PUBG·서든어택·배틀로얄에서 가져올 원리

### 공식 확인 사실

- [PUBG Update 15.2](https://pubg.com/en-sg/news/1356), 본문 기준 **2022-01-12 적용**: 기본 이동→아이템 사용→동료 소생으로 이어지는 단계형 튜토리얼, AI Training Match, 상황별 힌트, Training Helper, 개별 연습 공간. **2022년 당시 설계 자료이지 현재 모든 매치의 규칙이 동일하다는 뜻은 아니다.**
- [서든어택 조작법](https://guide.sa.nexon.com/guide/25), **2026-06-11 수정**: WASD, 걷기, 앉기, E 문열기/액션, 별도 특수행동, 상황판, 팀 채팅, 라디오 메시지.
- [서든어택 훈련소](https://guide.sa.nexon.com/guide/27), **2026-06-11 수정**: 브리핑 순서대로 기본 훈련, 자유 연습/자유시점/스톱워치, AI 난이도 4단계, 사람이 모자라면 아군 AI가 역할 보충.

### CHOOGuard 적용 해석 [INFERENCE]

| 참조 원리 | 가져올 가치 | 가져오지 않을 것 |
|---|---|---|
| 익숙한 FPS 이동 + 문맥 행동 키 | 카메라·이동을 익힌 뒤 공구와 장치 조작에 주의를 집중 | 사격 반동·헤드샷·살상 보상 중심 설계를 정비 성취와 동일시 |
| ping/라디오/상황판 | 시민 발견·지원 요청·역할 인계·경로 변경의 짧고 일관된 의사소통 | 실제 위험 구역/시설 취약점의 공격 활용이나 모사 |
| 기본훈련→AI 협동→본 세션 | 조작 숙련과 판단 난이도를 분리; AI가 빈 역할을 보충 | AI bot 전투 로직을 시민 대피·철도 업무 판단으로 이름만 바꿔 사용 |
| 한정된 세션·자원·상황 변화 | 시작 조건, 역할, 자원 제약, 종료 조건을 이해하기 쉽게 전달 | 배틀로얄 축소 원을 화재 확산 법칙처럼 사용, 인위적 긴박감을 실제 안전 규칙보다 우선 |
| 관전/복기 | 결과뿐 아니라 순서·협동·누락 원인을 돌아보기 | 결과 동영상만으로 누가 무엇을 알았는지/왜 거절됐는지 복원 가능하다고 가정 |

이 게임들의 공식 가이드·이미지는 적극적인 관찰 자료다. **공식 원본 소스/모델/음향의 재사용 패키지는 이번에 확보하지 못했다.** gameplay 원리를 독립적으로 재구성하는 조사와 유출 소스·상용 에셋의 무단 복사는 별개의 일이다. 라이선스 때문에 자료를 배제하지 않았지만, 공개 접근 우회나 권한 없는 다운로드는 하지 않았다.

## 7. NPC navigation·복기·개발 도구

### NPC navigation은 시민의 판단 모델 전체가 아니다

[AI Navigation 2.0 공식 본문](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html)은 이번 조회에서 **2.0.15 / 문서 생성 2026-09-24**를 반환했다. NavMesh, runtime dynamic obstacle, door/gap action을 연결하는 link를 설명한다. 관련 표면은 NavMeshSurface·NavMeshLink·NavMeshAgent 계열이며 정확한 현재 프로젝트 설치 여부는 조사하지 않았다.

**[INFERENCE]** 길찾기는 “갈 수 있는 경로”를 계산하는 층이고, 시민의 시야·정보 부족·도움 필요·열차/설비 접근 권한·명령 수용 여부는 다른 층이다. NavMeshAgent를 많이 스폰했다고 현실 피난 행동이 검증되는 것은 아니다. 공식 가이드의 door link는 문이 닫힘/잠김/사용 금지인지 같은 업무 상태와 별도로 연결해야 하는 연구 지점이다.

### Replay: 화면 재생·네트워크 재생·업무 증거는 다르다

[UE 5.6 Replay 공식 본문](https://dev.epicgames.com/documentation/unreal-engine/using-the-replay-system-in-unreal-engine?application_version=5.6)은 `DemoNetDriver`가 replication 데이터와 replay 전용 데이터를 streamer에 전달하여 저장·복원한다고 설명한다. `UGameInstance::StartRecordingReplay`, `StopRecordingReplay`, `PlayReplay`, `UDemoNetDriver::GotoTimeInSeconds`도 확인했다. 재생에 필요한 데이터가 replication 대상이어야 한다는 점이 핵심이다.

**[INFERENCE]** Unity든 Unreal이든 화면에 안 보이는 승인·거절 사유, 도구 교정 상태, 공구/부품 ID, 역할 인계, 시민이 실제로 관찰한 정보는 기록하지 않으면 복기에서 알 수 없다. 물리 seed만으로 장시간 재시뮬레이션을 보장한다고 가정하지 않는다. 이번 조사에서는 영구 복기 시스템의 구현·스키마를 확정하지 않는다.

### 이미 확인된 개발 도구 재료

- Boss Room: Multiplayer Play Mode, NetworkSimulator UI, NetworkStats/RTT, 세션 재접속 유틸리티.
- DOTS Character Controller tutorial: Multiplayer PlayMode Tools의 RTT 100ms 시뮬레이션 예시. 이는 문서 예시이지 이번에 측정한 수치가 아니다.
- Megacity: `NetcodePanelStats`.
- PhysicsSamples: query/거리/raycast의 **Scene view debug gizmo**와 mouse spring.
- Havok: Visual Debugger는 공식 문서상 Windows만 가능.
- AGXUnityScenes: joint·하중·cable/wire 비교, inspector를 통한 값 관찰.

이 자료들은 **관찰 가능성과 원인 분리 방법**을 제공한다. 이번 작업에서 실제 네트워크 지연·서버 tick·CPU/GPU·관절 안정성을 측정하지는 않았다.

## 8. 이미지·도해·영상 자료 장부

영상은 모두 **재생·타임스탬프 분석 미실시**다. 아래 “관찰 포인트” 중 영상 항목은 공식 제목·설명으로 판단한 관찰 목적이다. 이 분야는 실제 현장 사진보다는 조작 스크린샷·소스와 연결된 도해가 핵심이고, 철도 현장 사진은 다른 담당 보고서에서 다룬다.

| 자료/직접 링크 | 실제 확인 수준·날짜 | 무엇을 볼 수 있는가 / 한계 |
|---|---|---|
| [XRI 문·서랍 스크린샷](https://raw.githubusercontent.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/main/Documentation/Images/Station-09-PhysicsInteractables-Advanced.jpg) | **이미지 직접 확인**, 날짜 미표기 | 문손잡이/도어 두 종류와 열리는 서랍 배치. 관절 force/안정성은 정지 이미지로 증명되지 않음 |
| [XRI Physics Interactables 설명+기본 이미지](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/PhysicsInteractables.md) | 본문+이미지 링크 확인 | 키/socket/knob와 힘 기반 잡기의 연결 관계 |
| [My Summer Car 엔진룸](https://www.amistech.com/msc/game/02.jpg) | **이미지 직접 확인**, 날짜 미표기 | 화면 중앙 부품 명칭 `TWIN CARBURATORS`, 실제 3D 부품을 바라보는 관찰·선택 방식. 체결 메커니즘 전체는 보이지 않음 |
| [My Summer Car 차량 외관/HUD](https://www.amistech.com/msc/game/01.jpg) | **이미지 직접 확인**, 날짜 미표기 | 외관·작업 공간과 욕구/자원 HUD의 결합. 철도 UI에 그대로 복제할 근거는 아님 |
| [My Summer Car 공식 featured video](https://www.youtube.com/watch?v=r0IZ_TEzg7M) | 공식 사이트 embed 링크 확인·**미재생**, 영상 발행일 미확인 | 1인칭 생활/정비 게임의 흐름을 추가 관찰할 획득 경로 |
| [AGX Cable vs Wire 이미지](https://raw.githubusercontent.com/Algoryx/AGXUnityScenes/master/images/CableVSWire.png) | **이미지 직접 확인**, 날짜 미표기 | 하중을 단 두 시스템과 휘어진 선/곧은 선의 차이. 한 장으로 모델 정확도를 판단할 수는 없음 |
| [AGX Constraints 영상](https://www.youtube.com/watch?v=Y9smE0PpdF4) | 공식 AGXUnity README 제목 확인·**미재생**, 날짜 미확인 | 관절/제약 설정 워크플로 관찰 |
| [AGX Modelling a car](https://www.youtube.com/watch?v=bUTo3REt2f4) | 공식 README 제목 확인·**미재생** | 기계 조립체 구성과 동적 속성 설정 |
| [AGX CAD→crane tutorial](https://www.youtube.com/watch?v=YNEDk1417iM) | 공식 AGXUnityScenes 설명 확인·**미재생** | CAD 외형에서 joints/materials/rigid bodies/collision shapes를 부여하는 과정 |
| [Obi Cloth 영상](https://www.youtube.com/watch?v=ffuH_3DS0Is), [Rope](https://www.youtube.com/watch?v=kM36Q1m3jSA), [Fluid](https://www.youtube.com/watch?v=Gude_1WJJDQ) | 공식 제품 사이트 embed 확인·**미재생** | 천/호스/액체 상호작용을 별도로 비교할 자료. 광고 영상은 검증 보고서가 아님 |
| [Lyra Inside Unreal](https://www.youtube.com/watch?v=m80NJzUWq8A) | Epic 직원의 **2023-02-10** 공식 포럼 링크 확인·**미재생** | C++/Blueprint, 모듈형 프레임워크 설명 관찰 |
| [Lyra Fab 갤러리](https://www.fab.com/listings/93faede1-4434-47c0-85f1-bf27c0820ad0?lang=en) | 공식 상품 본문·7개 이미지 링크 확인, 개별 이미지 미열람 | 로비/전장/캐릭터·UI의 구조 관찰 및 프로젝트 획득 |
| [PhysicsSamples 도해·GIF 목록](https://github.com/Unity-Technologies/EntityComponentSystemSamples/blob/master/PhysicsSamples/README.md) | README와 장면별 GIF 링크 확인, GIF 미재생 | 질량·마찰·DOF·motor·ragdoll별 최소 실험 비교 |
| [Unity articulation 구조 도해](https://docs.unity3d.com/6000.0/Documentation/Manual/physics-articulations.html) | 공식 본문·도해 링크 확인, 문서 빌드 **2026-09-24** | parent-child tree와 regular joint 차이 |
| [NGO anticipation 시퀀스 도해](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects%402.7/manual/advanced-topics/client-anticipation.html) | 공식 본문·도해 링크 확인, 생성 **2025-10-31** | 두 사람이 동시에 변경할 때 시각 예상값과 서버 확정값의 차이 |
| [서든어택 훈련소 화면 모음](https://guide.sa.nexon.com/guide/27) | 공식 본문·스크린샷 링크 확인, 수정 **2026-06-11** | 단계 안내, 자유시점, 봇 수/난이도, 부족한 아군 역할 보충 |
| [PUBG 튜토리얼·훈련 화면](https://pubg.com/en-sg/news/1356) | 공식 본문·이미지 링크 확인, 적용 **2022-01-12** | 단계형 기초 훈련→AI 연습, 연습 도구·개인 공간 |
| [AI Navigation 2.0 영상 playlist](https://www.youtube.com/playlist?list=PLX2vGYjWbI0SsXFD1Gjo-8kFEpzk5k4Kh) | 공식 패키지 문서 링크 확인·**미재생** | NavMesh/동적 장애물/link의 엔진 작업 흐름 |

## 9. 라이선스·획득 메타데이터 — 검색 필터가 아닌 참고

**핵심 9개 후보의 실제 LICENSE 원문을 열람**했다. 법률 분석을 확대하지 않고, 코드와 모델/음원·제3자 에셋의 범위를 구분하는 정도로만 기록한다.

| 후보 | 원문 링크와 짧은 메모 |
|---|---|
| CharacterControllerSamples | [LICENSE.md](https://github.com/Unity-Technologies/CharacterControllerSamples/blob/master/LICENSE.md): Unity Companion License, 저작권 2022 |
| Boss Room | [LICENSE.md](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/LICENSE.md): Unity Companion License, 저작권 2021. NGO 패키지 자체와 샘플을 같은 라이선스 파일로 단정하지 않음 |
| Megacity Metro | [LICENCE.md](https://github.com/Unity-Technologies/megacity-metro/blob/master/LICENCE.md): Unity Companion License, 저작권 2024. `main` 경로는 404, `master` 원문 확보 |
| FPS Sample | [LICENSE.md](https://github.com/Unity-Technologies/FPSSample/blob/master/LICENSE.md): Unity Companion License, 저작권 2018 |
| Mirror | [LICENSE](https://github.com/MirrorNetworking/Mirror/blob/master/LICENSE): MIT, 저작권/허가 고지 유지 |
| FishNet | [LICENSE.md](https://github.com/FirstGearGames/FishNet/blob/main/LICENSE.md): 독자 약관. 게임 개발은 무상 사용 허용, 유사 네트워크 제품 이용 제한, Pro 팀 배포 조건. MIT로 부르면 안 됨 |
| Godot Jolt 확장 | [LICENSE.txt](https://github.com/godot-jolt/godot-jolt/blob/master/LICENSE.txt): MIT. core Jolt·Godot·제3자 항목의 개별 LICENSE 전체 감사는 하지 않음 |
| AGXUnity | [LICENSE](https://github.com/Algoryx/AGXUnity/blob/master/LICENSE): Apache-2.0 바인딩. README는 별도 유효 AGX Dynamics 라이선스를 요구 |
| MscModApi | [LICENSE](https://github.com/MarvinBeym/MscModApi/blob/master/LICENSE): MIT, 저작권 2021. My Summer Car 게임 본체·모델·DLL의 권리와 다름 |

- [Unity Companion 본문](https://unity.com/legal/licenses/unity-companion-license)도 직접 확인: **v1.4, 2024-10-29** 표기. 유효 Unity Engine License와 연계한 사용, 고지·제3자 조건이 핵심이다. 범용 MIT 소스처럼 타 엔진 이식을 전제하지 않는다.
- Lyra [Fab 본문](https://www.fab.com/listings/93faede1-4434-47c0-85f1-bf27c0820ad0?lang=en)은 **UE-Only Content**를 명시. 공식 [GitHub 안내](https://forums.unrealengine.com/t/lyra-starter-game-source-code-now-on-github/767395)는 Epic↔GitHub 계정 연결 절차를 제공한다. 계정 연결과 Fab Library→Create Project가 정상 획득 경로다.
- Starter Assets 새 상품은 **Non standard EULA** 표시만 확인, 패키지 내 원문 미확보. XRI/PhysicsSamples/Bitesize의 개별 LICENSE 원문은 이번에 별도로 확보하지 않아 다른 Unity 샘플과 같다고 단정하지 않았다.
- Obi는 공식 사이트에서 [Cloth](https://assetstore.unity.com/packages/tools/physics/obi-cloth-81333), [Rope](https://assetstore.unity.com/packages/tools/physics/obi-rope-55579), [Fluid](https://assetstore.unity.com/packages/tools/physics/obi-fluid-63067), [Softbody](https://assetstore.unity.com/packages/tools/physics/obi-softbody-130029) 유료 획득 경로를 확인했다. 가격과 현행 에셋 약관 원문은 확정하지 않았다.
- AGX는 [제품/문의 경로](https://www.algoryx.se/agx-unity/)와 공개 바인딩/샘플이 있다. 상용/계정 필요이므로 배제하지 않았으며, 현재 macOS arm64 배포 가능성은 별도 미확인 항목으로 남겼다.

## 10. 결론과 남은 불확실성

### 조사 우선순위에 대한 판단 — 구현 순서가 아님

- **가장 직접적인 내용:** Boss Room의 문/소유/세션 상태, XRI의 힘 기반 문·잡기·키/소켓, My Summer Car/MscModApi의 설치와 체결 상태 분리.
- **네트워크 대조군:** NGO/Boss Room은 협동 상태 처리, FishNet은 GameObject prediction, Entities OnlineFPS/Megacity는 ECS 기반 규모/예측 구조, Mirror는 보간·interest management를 비교하기 좋다.
- **상용이어도 가치 있는 자료:** Obi의 호스/천, AGX의 cable/wire/기구 모델링, Lyra의 계정 필요 소스·Fab 프로젝트를 모두 조사 대상에 포함했다.
- **과거 자료로 읽을 것:** FPS Sample, deprecated Starter Assets 개별 상품, 버전이 고정된 오래된 Godot/NGO 문서. 검색 상위라는 이유로 현재 기술 상태를 대표시키지 않는다.

### 구체적으로 미확인인 항목

1. 모든 후보의 **Unity 6000.3.23f1 + URP 17.3.0 + Input System 1.20.0 동시 호환성**, 패키지 충돌, IL2CPP/플랫폼 빌드.
2. 현재 CHOOGuard 코드의 실제 계층·결합도와 기존 NPC/세션/튜토리얼의 재사용 가능 범위.
3. 인터넷 지연 하의 문·긴 공구·두 사람 공동 잡기, late join 상태, host 이탈 시 실제 동작.
4. 시민 수·장치 수·네트워크 플레이어 수에 따른 성능. Megacity의 150인 소개는 이 프로젝트 성능을 보증하지 않는다.
5. AGX 현 Mac arm64/Unity 6.3 지원과 배포 라이선스, Obi 현재 상품 버전의 shader/backend 의존성.
6. Starter Assets/XRI/Obi 패키지 전체 원문과 Lyra 인증 저장소 내용. 공개 문서·일부 C# 원문만 열람했다.
7. 물리 시스템의 실제 철도 설비·작업 조건별 정확도, 화재/피난 validation. 해당 사실성을 이 조사만으로 승인하지 않는다.

**종합:** 현 투자 기준에서 핵심 질문은 “어느 FPS 엔진이 더 멋진가”보다 “이동/시점, 물체 상호작용, 업무 상태, 네트워크 확정, 시민 행동, 화재·유체 해석을 어디까지 실제로 연결해야 하는가”이다. 공개자료는 이들을 분리해 비교할 충분한 출발점을 제공하지만, 아직 특정 엔진 교체나 상세 개발계획을 확정할 근거는 아니다.

## 11. 추가 조사: 철도 시뮬레이터의 차량·제동·문 상태 코드

이 두 후보는 FPS 컨트롤러의 경쟁품이 아니라 **철도 도메인 모델의 보조 참고 자료**다. C#으로 쓰였어도 Unity 컴포넌트가 아니며, 에셋·노선·차량 데이터를 포함한 전체 이식은 검토하거나 확정하지 않았다. 과거 로컬 계획의 무기 없음·싱글플레이·역할 제한도 최신 사용자 승인으로 사용하지 않는다.

| 후보 | 분류·정확한 출처 | 확인된 구체 재료 | 유지보수·한계·라이선스 한 줄 |
|---|---|---|---|
| Open Rails | **차량/제동 모델 참고만**. [openrails/openrails](https://github.com/openrails/openrails), [공식 소스 다운로드](https://www.openrails.org/download/source/) | `Source/Orts.Simulation/Simulation/RollingStocks/SubSystems/Brakes/MSTS/SMEBrakeSystem.cs` 실제 원문. 차량별 제동/압력/밸브 상태와 디버그 출력. [Physics manual](https://open-rails.readthedocs.io/en/latest/physics.html)은 차량별 저항·질량·곡선·접착·연결기 유격을 설명 | 공식 다운로드 본문에 testing **2026-09-18**, stable 파일 **2026-01-14**. 페이지 제목은 1.5.1이나 링크는 1.6.1이라 버전 표기 불일치. 소스 헤더 GPL-3.0-or-later. 공식 사이트는 오락용이며 전문 용도에 부적합하다고 명시 |
| OpenBVE | **문/차량 상태 모델 참고만**. 현재 README가 지시하는 [leezer3/OpenBVE](https://github.com/leezer3/OpenBVE), [공식 사이트](https://openbve-project.net/) | `source/TrainManager/Train/Doors.cs`, `source/TrainManager/Car/Door.cs` 실제 원문. 좌우 문, 개폐 상태, interlock, 정차 위치 조건, 닫힘/재개방·장애물 상태 | [v1.14.0.3 공지](https://openbve-project.net/intro/V1.14.0.3/) **2026-08-10** 확인. OpenGL/OpenTK 기반 cab simulator, Unity와 별도. README는 원래 public-domain 지향·신규 코드 BSD-2 또는 유사 permissive와 파일 헤더 확인을 안내; 단일 MIT라고 하면 틀림 |

### Open Rails에서 확인한 것

[SMEBrakeSystem.cs](https://github.com/openrails/openrails/blob/master/Source/Orts.Simulation/Simulation/RollingStocks/SubSystems/Brakes/MSTS/SMEBrakeSystem.cs)는 `SMEBrakeSystem : AirTwinPipe`, `Update(float elapsedClockSeconds)`, `GetDebugStatus(...)`, `GetFullStatus(...)`를 포함한다. 코드에 제동 압력, holding valve 상태, 압력 변화율, 차륜 미끄럼 방지 상태 및 단위별 표시가 분리되어 있다. 해당 SME 방식의 역사적 문헌 링크도 코드 주석에 있다. **이 클래스는 특정 제동 계열이지 KTX·도시철도 전체 제동 장치의 표준 모델이 아니다.**

[공식 Physics manual](https://open-rails.readthedocs.io/en/latest/physics.html)에서 확인한 중요한 내용은 `.wag`/`.eng` 데이터와 `ORTS` 추가 파라미터, 차량별 저항 계산, empirical 모델의 저속 한계와 입력값 의존성이다. 일부 접착 모델은 프레임률에 따라 계산법을 바꾸는 설명도 있다. **[연구판단]** 결과의 재현성과 판정 공정성이 중요하다면 오락용 솔버의 적응형 단순화를 그대로 복사할 수는 없다. “유명 철도 시뮬레이터의 코드”라는 이유만으로 실제 차량에 대한 검증이 끝났다고 볼 수 없다.

### OpenBVE에서 확인한 것

- [Train/Doors.cs](https://github.com/leezer3/OpenBVE/blob/master/source/TrainManager/Train/Doors.cs): `TrainBase.OpenDoors`, `CloseDoors`, `GetDoorsState`, `AttemptToOpenDoors`, `AttemptToCloseDoors`, `UpdateDoors`.
- [Car/Door.cs](https://github.com/leezer3/OpenBVE/blob/master/source/TrainManager/Car/Door.cs): `Door.Direction`, `State`(0–1), `DoorLockState`, `DoorLockDuration`, `AnticipatedOpen`, `AnticipatedReopen`, `ReopenCounter`, `ReopenLimit`, `InterferingObjectRate`, `Width`, `MaxTolerance`.
- 원문에서 문 명령은 좌/우 interlock 상태와 구분되고, 완전 열림/닫힘/중간 상태를 집계하며, 정차 위치 허용 범위와 출발 시각에 따른 자동 문 동작을 처리한다. `AnticipatedOpen`은 여기서 문 동작의 목표/예상 상태에 관한 이름이며 **NGO의 네트워크 anticipation과 같은 시스템이 아니다.**
- **[연구판단]** 이 자료는 CHOOGuard의 “문이 열렸다”를 단일 애니메이션 이벤트로 다루지 않고, 명령·물리 개방 정도·차량별 집계·권한/인터록·실패 원인을 분리해 볼 구체 출발점이다. 실제 한국 차량의 interlock·장애물 판단 기준·정차 오차 값을 이 코드에서 그대로 가져오지는 않는다.

### 도해·추가 관찰 경로

- [Open Rails Physics manual의 접착 곡선·HUD 도해](https://open-rails.readthedocs.io/en/latest/physics.html): 본문과 도해 링크 확인, 이미지 개별 열람/시뮬레이터 실행 미실시. 물리 상태를 수치·경고와 함께 노출하는 방식을 볼 자료.
- [OpenBVE 개발자 문서](https://openbve-project.net/documentation_hugo/en/): README에서 링크 존재 확인, 세부 본문은 이번에 미열람. 차량·노선·애니메이션 데이터 정의를 더 확인할 정상 획득 경로.
- 두 시뮬레이터 모두 **프로그램 소스 공개와 노선/차량/음원 에셋 공개 범위는 별개**이며, 본 조사에서는 원본 코드 일부와 공식 설명만 열람했다.
