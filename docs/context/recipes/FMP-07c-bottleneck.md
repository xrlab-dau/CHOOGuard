# FMP-07c — 접촉·밀집·정지 병목 재현 절차

Issue #127 (FMP-07c). 이 문서는 선언된 연결 세계(connected world)의 병목에서 접촉·밀집·정지와
동일 geometry epoch 복원 불변식을 **다시 측정**하는 절차다. 수치는 기억이 아니라 실행 로그에서 읽는다.

---

## 1. 기하 앵커

### 1.1 선언 프로파일 (벽·구역 외피의 출처)

| 항목 | 값 |
|---|---|
| 프로파일 | `foundation/world/connected-world-profile.json` |
| sha256 | `e2cae663ce04b63b197c99dda7a26a394619430e47cb071834254c3d23f94cba` |
| ProfileId | `native-connected-v1` |
| Classification | `synthetic_design_not_facility_acceptance` |
| 토폴로지 | 13 regions / 12 portals / 3 frames |
| frames | `world`, `train-mainline`, `train-metro` |

병목은 세 개의 선언 구역과 두 개의 선언 포털로만 이루어진다. 좌표를 발명하지 않는다.

| 구역 / 포털 | 선언 값 |
|---|---|
| `underground_connector` | center (129.8, -4, 0), 99.6 × 8.0, `WallHeight` 3.2, 바닥 Y = -4 |
| `underground_connector--underground_shopping_passage` | `FromPoint` X = 179.6 → `ToPoint` X = 196.0, `ClearWidth` 3.2, `ClearHeight` 2.7 |
| `underground_shopping_passage` | center (210, -4, 0), 28 × 12, 바닥 Y = -4 |
| `rail_terminal_public--underground_connector` | (56, 6) → (80, -4), 기울기 **-10/24** (지원 평면 시험용) |
| `rail_platforms_mainline` | 바닥 Y = 2, `WallHeight` 1.15 (난간 정지 시험용) |
| `station_concourse_2f` | 바닥 Y = 6 (rebind 지원 변경 시험용) |
| `rolling_stock_metro` | `train-metro` 프레임 차량 동체 (공간 분리 시험용) |

프로파일은 **float32** 로 저장된다. 따라서 `99.6f` 는 `99.5999984741211` 로 읽히고
`3.2f` 는 `3.200000047683716` 로 읽힌다. 시험의 선언값 가드는 밀리미터 스케일
`DeclaredToleranceM = 1e-4` 를 쓴다. 기울기 비율만 `RatioTolerance = 1e-6` 을 쓴다.

### 1.2 전임 입력 — #125 (FMP-07a) world-actual fixture

#127 의 선언된 전임 입력은 `artifact:125:world-crowd-fixture:candidate`, 즉
`foundation/tests/FMP-07a/world-crowd-fixture.json` 이다. **이 파일은 이 체크아웃에 존재하며
시험이 실제로 소비한다** (`DeclaredPredecessorFixtureIsConsumedAsWorldActualGeometryAndNotAsTheAbstractLaboratory`).

| 항목 | 값 |
|---|---|
| 경로 | `foundation/tests/FMP-07a/world-crowd-fixture.json` |
| `FixtureKind` | `world_actual_collider_nav` |
| `Body.RadiusM` | **0.41** = `WorldBodyPresentation.NpcRadiusM` |
| `Body.AbstractUnitRadiusM` | **0.15** (별도 종류, 혼동 금지) |
| `Body.Count` | 100 (`PlayerCount` 0) |
| `SourceProfile.Sha256` | `e2cae663…f94cba` (1.1 의 프로파일과 동일) |
| `SourceProfile` 토폴로지 | 13 regions / 12 portals / 3 frames |
| `SceneHash.ManifestSha256` | `830d9bfadcaace0d768360ed5f43be3d8fbaa605ed7b34c1ad3cbfb9abed9541` |
| `Placement` | 13 구역, 구역별 8·8·8·8·8·8·8·8·8·7·7·7·7 = 100, `SlotSpacingM` 0.90 |
| `GeometryLoad` | 13 구역 씬을 정확히 한 번씩, 모든 저작 콜라이더가 존재·활성·불변 |

**두 반지름은 서로 바꿔 쓸 수 없다.** 0.15 m 는 실험실 보정 단위(abstract unit)이고, 0.41 m 는
저작 Evacuee 동체 크기(world actual)다. #125 음성 계약이 이를 명시적으로 금지한다.
이 레시피에서 **선언 프로파일 시나리오는 0.15 m**, **world-actual 시나리오는 0.41 m** 를 쓴다.

**동결 상태에 대한 관측 사실.** 이 전임 파일은 다른 작업 항목의 산출물이며 이 저장소에서
추적되지 않는다(`git status` 에서 untracked). 따라서 시험은 이 파일의 sha256 을 상수로
박아 넣지 않는다 — 낡은 상수는 거짓 앵커가 된다. 대신 시험은 **계약 필드값**(FixtureKind,
RadiusM, 구역 명부, manifest 해시, geometry-load 계약)을 단언하고, **이번 실행이 실제로 읽은
바이트 수와 sha256 을 영수증 `fmp07c-world-actual-fixture.json` 에 기록**한다.
소비자는 영수증의 `PredecessorSha256` 으로 어느 바이트를 소비했는지 확인한다.

---

## 2. 실행 명령

```bash
EVIDENCE_DIR=<scratchpad>/fmp07c
/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testFilter "ChooGuard.Foundation.Tests.CrowdWorldContactTests;ChooGuard.Foundation.Tests.PhysicalCheckpointTests;ChooGuard.Foundation.Tests.Fmp07cContactRestoreTests" \
  -testResults "$EVIDENCE_DIR/FMP-07c-edit.xml" -logFile "$EVIDENCE_DIR/FMP-07c-unity.log"
```

Unity 는 프로젝트당 한 인스턴스만 허용한다. 다른 인스턴스가 `Temp/UnityLockfile` 을 쥐고 있으면
"another Unity instance is running with this project open" 으로 즉시 중단되므로, 락이 풀린 뒤 실행한다.

시험 결과는 `Temp/` 가 편집기 종료 시 삭제되므로 로그의 `FMP07C-RECEIPT <name> <json>` 줄에서 읽는다.

---

## 3. 재현 파라미터

| 파라미터 | 값 |
|---|---|
| seed | **127** (`RecipeSeed`) |
| body count | **24** (`BottleneckRecipeReproducesOnceWithRegionCountSeedAndGeometryHash`) |
| 선언 프로파일 NPC 반지름 | 0.15 m (150 mm) — abstract unit |
| world-actual NPC 반지름 | **0.41 m** — `foundation/tests/FMP-07a/world-crowd-fixture.json` 에서 시험 시점에 읽음 |
| 플레이어 반지름 | 0.30 m |
| 동체 높이 | 1.8 m |
| tick | 0.05 s, `MaxSubstepSeconds` 0.01 → tick 당 5 substep |
| tick 수 | 400 → 20.0 s |
| 초기 배치 | `npcNN` at X = 176 + (i%6)·0.32, Z = -1.2 + (i/6)·0.8 |
| 의도 | `Goal` = (400, Z), `PreferredSpeedMS` 1.4 |
| fixture 정의 | 선언 구역 외피에서 파생한 벽 목록 |
| fixture geometry hash | `71dfe824526d80482e4a735e9f6ea995117b03246f6c270050e45e37bed9d52f` |
| 접촉 허용오차 | `ContactToleranceM` = 1e-3 m (1 mm) |
| 접촉 시나리오의 모델 | contact-only 변형 — `PersonRepulsion` = 0 (계수만 0, 범위 0.1 은 그대로) |
| 선언 필드의 실측 정지 간극 | world-actual 0.41 m 봉쇄에서 **0.016974 m** — 접촉이 아니다. 0.15 m 실험실 반지름 봉쇄의 선언 필드 정지 간극은 **측정하지 않았다** |

`ContactOnly` 는 **계수** `PersonRepulsion` 만 0 으로 둔다. 범위를 0 으로 두면 안 된다 —
`CrowdMotionModel.Validate` 가 `Positive(PersonRepulsionRangeM)` 을 요구하므로 그 정의는 생성자에서
거부된다. 계약이 요구하는 것은 `Nonnegative(PersonRepulsion)` 뿐이므로 계수 0 은 유효하다.

선언 필드(계수 5, 범위 0.1)에서는 지수항 `5·exp(−간극/0.1)` 이 단위 목표 방향을 앞지르는 지점
`0.1·ln 5 = 0.161 m` 아래로 간극이 내려가면 동체가 다시 밀려나므로, **유한 시간 안에 접촉에 도달할 수
없다.** 그래서 접촉 주장은 contact-only 변형에서 측정하고, 선언 필드가 만드는 정지 간극은
`DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact` 가 별도로 측정한다.

벽 목록은 선언 구역에서 파생된다: `connector-north/south`(Z = ±4), `connector-west-south/north`(X = 80),
`connector-east-south/north`(X = 179.6), `choke-south/north`(Z = ±1.6, X 179.6→196),
`passage-north/south`(Z = ±6), `passage-west-*`(X = 196), `passage-east-*`(X = 224),
`platform-railing-north/south`(Z = ±5, 상단 Y = 3.15), 차량 3면(`train-metro`).
`FixtureWallsAreDerivedFromTheDeclaredRegionsAndReproduceTheDeclaredClearWidth` 가 모든 벽 정점이
선언 구역 외피 안에 있음을 확인하므로, 발명된 좌표가 없다.

### 3.1 world-actual 정지 시나리오의 봉쇄 기하 (반지름 0.41 m)

`WorldActualRadiusColumnContactsTheDeclaredChokeBlockageWithoutPenetration` 는 선언 choke(clear width 3.2 m)
안에 **world-actual 반지름 0.41 m** 의 pin 3체를 정확히 한 지름(2r = 0.82 m) 간격으로 세운다.

| 값 | 식 | 수치 |
|---|---|---|
| pin 간격 | `2r` | 0.82 m (접촉, 비관통) |
| 교각 여유 | `1.6 − 3r` | 0.37 m |
| 통과에 필요한 폭 | `2r` | 0.82 m |
| 결론 | `0.37 < 0.82` | 통과 불가 |

0.15 m 반지름으로 같은 choke 를 막으려면 pin 을 2r = 0.30 m 간격으로 세워야 하는데, 그 경우
3.2 / 0.30 = 10.67 로 정수배가 맞지 않아 교각 쪽에 통과 가능한 틈이 남는다. 반면 0.41 m 에서는
`3.2 / 0.82 = 3.90` 이고 교각 양쪽에 남는 0.37 m 가 필요 폭 0.82 m 보다 좁아 봉쇄가 성립한다.
이 계산은 시험 안에서 `ChokeHalfWidthM − 3r < 2r` 로 단언된다.

이 시나리오는 **contact-only 변형**(§3 표)에서 돌린다. 같은 기하·같은 지령을 선언 필드로 돌리면
종대는 봉쇄 앞에서 멈추지만 **접촉에는 도달하지 않는다** — 실측 정지 간극 0.016974 m 다. 두 측정은
`WorldActualRadiusColumnContactsTheDeclaredChokeBlockageWithoutPenetration`(접촉)과
`DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact`(비접촉)가 나눠 맡고,
두 영수증이 그 값을 각각 기록한다.

---

## 4. 실행할 시험 (21개, 파일 선언 순서)

| 시험 | 고정하는 것 |
|---|---|
| `DeclaredProfileStillCarriesTheThirteenRegionTwelvePortalDeclaredChoke` | 프로파일 바이트 해시·토폴로지·선언 치수 |
| `FixtureWallsAreDerivedFromTheDeclaredRegionsAndReproduceTheDeclaredClearWidth` | 벽이 선언 외피 안, choke 폭 재현, 파생 결정성 |
| `DenseColumnEnteringTheDeclaredChokePassesWithoutPenetration` | 밀집 종대의 통과·비관통 (매 tick) |
| `LocalDensityIsCappedByTheHardDiscBoundAndCongestionActuallyForms` | 국소 밀도 상한 = 비관통(매 tick) |
| `AJammedDeclaredChokeRunsSlowerThanTheSameCorridorAtFreeSpeed` | 정체 대비 자유 보행 속도 + **접촉 단언**(contact-only 변형) |
| `WallAndRailingStopBodiesOnTheDeclaredEnvelopeOnly` | 벽·난간 정지가 선언 외피에서만 일어남 |
| `PinnedBodiesHoldUnderContactFromTheDeclaredCorridorColumn` | pin 은 정확 구속(밀려나지 않음) |
| `SameEpochRestoreRefusesToChangePinOrSupportBinding` | **동일 epoch 복원은 pin/support 변경 불가(18건)** |
| `SameEpochRestoreAcceptsOnlyPosesOnTheIdenticalSupportPlane` | 같은 지원 평면 위 자세만 복원 허용 |
| `FailedTickIsNotPartiallyCommittedAndTheStateIsByteIdentical` | **부분 tick commit 금지** |
| `NonfiniteOverlappingAndOutOfRangeInputsAreRejected` | **nonfinite·겹침 입력 거부(생성자 17건)** |
| `ExplicitRebindIsTheOnlyGeometryEpochThatMayChangeSupport` | 지원 변경은 rebind 로만, epoch +1 |
| `RebindAndRestoreRejectStaleOrForeignRequestsAtomically` | stale/foreign 요청 원자 거부 |
| `LegacyCheckpointCannotBeAdoptedByTheDeclaredGeometry` | CGC1 legacy 를 선언 기하가 채택 불가 |
| `TwoModelsWithTheSameSeedAndCommandProduceTheSameBottleneckState` | 동일 seed·명령 바이트 재현 |
| `StepSensitivityAcrossDeclaredSubstepSizesIsRecorded` | substep 민감도(주장이 아니라 기록, 매 tick) |
| `ReverseFlowThroughTheDeclaredChokeTraversesWithoutPenetration` | 역방향(대피) 유동 (매 tick) |
| `BottleneckRecipeReproducesOnceWithRegionCountSeedAndGeometryHash` | **이 문서의 레시피 영수증** |
| `DeclaredPredecessorFixtureIsConsumedAsWorldActualGeometryAndNotAsTheAbstractLaboratory` | **전임 #125 fixture 소비·계약 검증·반지름 분리** |
| `WorldActualRadiusColumnContactsTheDeclaredChokeBlockageWithoutPenetration` | **world-actual 0.41 m 접촉·정지** (매 tick, contact-only 변형) |
| `DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact` | **선언 필드의 비접촉 음성 대조**(매 tick) |

표의 순서는 파일 선언 순서와 같다. 서수 열은 두지 않는다 — 행을 이름으로 키잉하므로 파일에서
순서가 바뀌어도 표는 그대로 유효하다.

**매 tick 비관통을 재계산하는 시험은 8개다**: `DenseColumn…`·`AJammed…`·`ReverseFlow…` 는
`StepCommittedChecked` 로 매 tick `AssertNoPenetration` 을 부르고, `StepSensitivity…`·
`WorldActualRadiusColumn…`·`DeclaredPersonRepulsion…` 는 본문 루프에서 직접 부르며,
`LocalDensity…` 와 `BottleneckRecipe…` 는 매 tick 의 스냅샷에서 최소 중심 간격을 누적해 그 최솟값에
단언한다. **나머지 13개 시험은 창 끝에서 한 번 단언하거나 애초에 운동 시나리오가 아니다** —
이 문서는 그 이상을 주장하지 않는다.

레시피 재현의 단일 진입점은 `BottleneckRecipeReproducesOnceWithRegionCountSeedAndGeometryHash` 다.

### 4.1 음성 사례(Negative Cases) 회귀 방어 표

정상 경로만 도는 시험은 회귀를 잡지 못한다. 아래는 "무엇이 **거부되어야** 하는가"를 고정하는
단언들이며, 각 음성 사례는 **거부 자체**와 **거부 후 상태 불변**을 함께 단언한다.

| 음성 사례 | 시험 | 단언되는 거부 | 거부 후 상태 |
|---|---|---|---|
| 잘못된 동체 입력 | `NonfiniteOverlappingAndOutOfRangeInputsAreRejected` | 생성자 `ArgumentException` **17건** — nonfinite 위치·속도·의도속도·발높이, 범위 밖 발높이, 단위 초과 지지기울기, 지름 미만 높이, 속도 있는 pin, 하한 미만 반지름, 선언 상한 초과 속도, 미정의 `IntentMode`, 겹친 명부, 중복 id, 수직 겹침, choke 교각면 위 동체, 벽 안 동체, 길이 0 벽 | 생성 자체가 실패하므로 부분 상태가 존재하지 않는다 |
| 잘못된 tick 입력 | `NonfiniteOverlappingAndOutOfRangeInputsAreRejected` | `TryAdvance` 가 `false` **7건** — NaN·0·음수·과대 dt, NaN 의도, 미등록 agent, 선언 지지창 밖 이동 | `ExportCheckpoint()` 가 **바이트 동일**, `Tick` 은 0 유지 |
| 동일 epoch 복원으로 결속 변경 | `SameEpochRestoreRefusesToChangePinOrSupportBinding` | 같은 `GeometryRevision` 에서의 변경 **18건** 거부 — pin, support gradient, surface id, region id, frame id, contact space, 평면 밖 발높이 ×2, 반지름, 높이, 선호속도, seed, 명부, geometry revision, 겹친 자세, nonfinite 위치·속도·발높이 | `CheckpointByteIdenticalAcrossAllRejections` = true, revision 0 유지 |
| 부분 tick commit | `FailedTickIsNotPartiallyCommittedAndTheStateIsByteIdentical` | 거부된 tick 은 fork 에만 투영되고 live 상태에 할당되지 않는다 | 상태 바이트 동일, 복구 tick 이 손대지 않은 쌍둥이와 일치 |
| stale·foreign 재결속 | `RebindAndRestoreRejectStaleOrForeignRequestsAtomically` | 낡은 revision·다른 기하의 요청 | 원자 거부 — 부분 적용 없음 |
| legacy checkpoint 채택 | `LegacyCheckpointCannotBeAdoptedByTheDeclaredGeometry` | CGC1 legacy 를 선언 기하가 채택하는 것 | live 모델 불변 |
| 반지름 종류 혼동 | `DeclaredPredecessorFixtureIsConsumedAsWorldActualGeometryAndNotAsTheAbstractLaboratory` | 0.15 m abstract unit 을 world-actual 0.41 m 로, 또는 그 반대로 쓰는 것 | `Is.Not.EqualTo` 로 두 종류 분리 단언 |
| 접촉을 주장하는 비접촉 | `DeclaredPersonRepulsionHoldsApproachingBodiesAtASoftStandOffRatherThanContact` | 선언 필드에서 접촉에 도달했다는 주장 | 실측 정지 간극 0.016974 m 를 영수증 `fmp07c-soft-standoff.json` 에 기록 (음성 대조) |
| 관통·겹침 | `DenseColumn…`·`AJammed…`·`ReverseFlow…`·`LocalDensity…`·`WorldActualRadiusColumn…` | 매 tick 최소 중심 간격이 반지름 합 미만이 되는 것 | 매 tick `AssertNoPenetration` |
| 기하 epoch 무단 변경 | `ExplicitRebindIsTheOnlyGeometryEpochThatMayChangeSupport` | rebind 아닌 경로로 support 를 바꾸는 것 (`TryRestore(before)` 가 `Is.False`) | epoch 는 rebind 로만 +1 |
| 복원 후 자세 이동 | `SameEpochRestoreAcceptsOnlyPosesOnTheIdenticalSupportPlane` | 같은 지원 평면 밖의 자세 | 평면 밖 발높이 거부 |

표의 "거부 후 상태" 열이 비어 있는 음성 사례는 없다 — 모든 거부가 상태 불변을 함께 단언한다.

---

## 5. 기록되는 영수증

로그에서 `FMP07C-RECEIPT` 로 시작하는 줄을 읽는다. 각 영수증은 `WorkId`, `Command`,
`DeclaredGeometryHash` 를 함께 실어 어떤 실행에서 나온 값인지 자기 서술한다.

- `fmp07c-bottleneck.json` — 레시피 본 측정
- `fmp07c-density-speed.json` — 자유/정체 속도 대비 + `MinimumClearanceToPinnedM`·`ContactAsserted`
- `fmp07c-restore-rejections.json` — 동일 epoch 복원 거부 18건과 사유
- `fmp07c-atomicity.json` — 실패 tick 의 원자성
- `fmp07c-step-sensitivity.json` — substep 민감도 밴드
- `fmp07c-world-actual-fixture.json` — 소비한 전임 #125 fixture 의 경로·바이트 수·sha256·계약값
- `fmp07c-world-actual-contact.json` — world-actual 반지름 접촉·정지 측정 (contact-only 변형)
- `fmp07c-soft-standoff.json` — 선언 필드가 만든 비접촉 정지 간극(접촉 케이스의 음성 대조)

영수증은 `Temp/ChooGuardCrowdBottleneck/` 에도 쓰이지만 `Temp/` 는 편집기 종료 시 삭제된다.
로그가 정본이다.

---

## 6. 판독 시 주의

- **밀도 상한은 비관통 자체다.** 16.4 m choke 전체의 면적 평균(약 0.38 /m²)은 육방 최밀
  충전 한계(0.15 m 반지름에서 약 12.8 /m²)보다 훨씬 낮아 그 자체로는 결함을 잡지 못한다.
  실제로 구속하는 것은 매 tick 의 최소 중심 간격이며, 시험은 그쪽을 단언한다.
- **접촉은 이름이 아니라 측정으로 주장한다.** `MinimumClearanceToPinnedM` 이
  `ContactToleranceM`(1 mm) 이내일 때만 그 시나리오를 접촉 사례라고 부른다. 간극이 남아 있으면
  시험 이름과 주장을 접촉이 아닌 것으로 고친다. `DenseColumn…` 은 종대 간격이 반지름 여유를
  남기므로 **접촉을 주장하지 않는다** — 그래서 이름이 `PassesWithoutPenetration` 이다.
- **접촉 수치는 contact-only 변형의 것이고, 선언 필드의 수치는 아니다.** 두 값을 섞어 읽으면
  거짓말이 된다. 실측: 선언 필드(계수 5, 범위 0.1)에서 world-actual 0.41 m 봉쇄의 정지 간극은
  **0.016974 m** 다 — 이것이 이 작업이 측정한 **유일한** 선언 필드 정지 간극이다. 0.15 m 실험실
  반지름 봉쇄에 대해서는 선언 필드 정지 간극을 **측정하지 않았으므로 수치를 적지 않는다.**
  contact-only 변형(계수 0)의 최소 중심-대-pin 간극은 world-actual 0.41 m 봉쇄에서 **0.000350 m**,
  선언 player 0.3 m pin 정체(`fmp07c-density-speed.json`)에서 **0.000457 m** 로 둘 다 1 mm
  이내다. 영수증은 각각 `fmp07c-soft-standoff.json`(비접촉)과
  `fmp07c-world-actual-contact.json`·`fmp07c-density-speed.json`(접촉)에 남는다.
- **`PersonRepulsion` 을 0 으로 두는 것은 물리 가정을 하나 끄는 일이다.** 이 변형에서 비관통을
  지키는 것은 CSM 시간간격 속도 상한 `(거리 − 반지름합)/TimeGapSeconds` 뿐이다. 그 사실을
  영수증 `Notes` 에 적었고, 선언 필드가 그 자리에서 무엇을 하는지는 별도 시험이 측정한다.
- **substep 민감도는 등식이 아니라 밴드다.** 시험은 두 substep 크기의 최대 변위차를 0.25 m 이내로
  묶고 측정값을 영수증에 남긴다. 측정된 값이 0.0 이라고 해서 불변식이 0.0 인 것은 아니다.
- **`MaximumGroundSpeedMS` 는 설계 속도가 아니라 솔버가 보고한 지상 속도의 최대값이다.**
- **`GeometryRevision` 은 0 에서 시작한다.** 그래서 "stale revision" 은 `0` 이 아니라
  `현재 + 1`(또는 성공한 rebind 로 epoch 를 올린 뒤의 이전 값)로 만들어야 한다. 0 은 stale 이 아니다.
- **프로세스 간 비트 동일성은 시험하지 않았다.** 같은 시험 파일의 `ExportCheckpoint()` 문자열은
  한 프로세스 안에서 두 모델이 정확히 일치함을 단언한다. Unity `JsonUtility` 의 17자리 표기와
  이 문서/결과의 최단왕복 표기는 **같은 IEEE-754 double 의 서로 다른 직렬화**이지 드리프트가 아니다.

---

## 7. 이 레시피가 주장하지 않는 것

- **장면 콜라이더 인스턴스 캡처가 아니다.** 병목의 **벽**은 공개 선언 프로파일의 구역 외피와
  포털 clear width 에서 파생하며, 13개 구역 씬을 적재해 `ConnectedWorldGeometry.Capture` 로
  콜라이더 인스턴스를 뜨는 경로는 #125 (FMP-07a) 소관이다. 이 체크아웃에는 그 캡처 **실행**이
  없다 — 다만 #125 의 **계약 산출물**(`foundation/tests/FMP-07a/world-crowd-fixture.json`)은
  존재하며 이 작업이 그것을 소비한다: 저작 동체 반지름 0.41 m, 13 구역 씬 명부, geometry-load 계약.
  캡처된 콜라이더 인스턴스 자체를 재캡처하지는 않는다.
- **시설·철도·현장 인수 시험이 아니다.** `Classification` 이 `synthetic_design_not_facility_acceptance` 다.
- **20 Hz 전체 서버 프레임 예산은 측정하지 않았다.**
- 이 문서의 수치는 **이 호스트**(Apple M1, macOS arm64) 한 번의 실행에서 나온 값이다.
  다른 장비의 수치와 직접 비교하지 않는다.
