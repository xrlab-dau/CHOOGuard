# Findings

## 슬라이스 배치 메뉴가 기존 소화기 11개를 지운다 (2026-09-24, 실제로 겪음)

**위험도: 높음. 누구든 메뉴를 한 번 누르면 발생한다.**

`ChooGuard/수직 슬라이스/소화기 월간점검 배치` 메뉴를 실행하면 이렇게 된다.

```
[슬라이스] 배치 1개 · 기존 제거 13개
FacilityInspectable  12 → 1
FpsStation.unity     1,757 삽입 / 5,621 삭제
```

원인은 메뉴가 **단일 배치 경로**를 부르는 것이다.

```csharp
[MenuItem("ChooGuard/수직 슬라이스/소화기 월간점검 배치")]
public static void BuildMenu(){BindMaterials();Build(true);}
// → Build(bool) = "기존 단일 배치 경로. 플레이어 정면 1.2m 에 하나를 두던 동작을 그대로 보존한다."
```

다중 배치 오버로드 `Build(IReadOnlyList<Placement>, bool)`는 존재하지만 **저장소 어디에서도 호출하지 않는다.** 유일한 호출은 단일 경로가 자기 자신을 `Build(new[]{one}, saveScene)`로 감싸는 139줄뿐이다.

`Build(placements, ...)`는 시작할 때 `RootName`("튜토리얼 · 소화기 월간점검")과 `UnitPrefix`("소화기 · ")에 걸리는 기존 오브젝트를 전부 제거한 뒤 새로 배치한다. 확인 절차나 경고가 없다.

### 12개는 어디서 왔나

#237이 배치한 소화기 12개는 이 생성기가 만든 것이 아니다. 좌표 자료는 `.planning/2026-09-22-station-interior-build/`에 `extinguisher-placement.json`, `-v2.json`, `-v3.json`으로 남아 있고, 생성기 주석도 "NFTC 101 보행거리·구획 산정 결과(extinguisher-placement-v3.json)를 그대로 받기 위한 것"이라고 적고 있다. 그러나 **그 자료를 읽어 다중 배치를 호출하는 코드는 없다.** 즉 씬의 12개는 코드로 재현되지 않는 상태다.

같은 디렉터리의 `jev-inject-structure-005.input.json`이 이 공백을 이미 적어 두었다.

> "Inject 12 regulation-derived extinguisher coordinates into Editor/FireExtinguisherSliceBuilder.cs, which today builds exactly one unit."

계획은 있었고 반영되지 않았다. 나는 이 기록을 grep 결과로 화면에서 보고도 읽지 않은 채 메뉴를 실행했다. 씬은 커밋본에서 복원했으므로 영구 손실은 없다.

### 해소 (2026-09-24)

세 권고를 모두 반영했다.

1. **12개 좌표 주입 완료.** `PlacementsV3` 정적 배열을 생성기에 추가하고 `BuildMenu`가 이것을 배치하게 했다. 출처는 `extinguisher-placement-v3.json`(schema `chooguard.extinguisher-placement.v2`, computedAt 2026-09-22, NFTC 101 + `jev-poi-placement-004`). 실행 결과 `배치 12개`, 씬의 `FacilityInspectable` 12개 유지, PlayMode 45/45.
2. **기본 메뉴가 이제 역사 배치를 재현한다.** 메뉴 이름도 `소화기 월간점검 배치 (역사 12개)`로 바꿔 무엇이 일어나는지 드러냈다. 사고가 구조적으로 재발하지 않는다.
3. **단일 배치에 안전장치.** `개발용 · 플레이어 앞 1개만 배치` 메뉴로 분리하고 `EditorUtility.DisplayDialog`로 "역사에 배치된 소화기를 모두 제거하고 1개만 둡니다"라고 확인받는다.

**이제 씬이 코드로 재현된다.** 이전에는 12개가 어떻게 들어갔는지 저장소에 없었다.

### 주입하며 기록한 불일치 두 가지

코드 주석에도 같은 내용을 적었다.

| 항목 | 자료(v3) | 생성기 | 판단 |
|---|---|---|---|
| 설치 높이 | 바닥 +1.2m (`mountY`) | 바닥 +1.10m (`BuildUnit`) | 둘 다 NFTC 101 의 1.5m 이하 안. 규정 위반은 아니나 자료와 10cm 다르다 |
| 고유번호 | 없음 | `BSN-CONC-FE-001`~`-012` | **작성한 값이며 출처가 없다** |

월드 상태(부식·기한)도 튜토리얼 시나리오 조건이지 실측이 아니다. 튜토리얼 대상 1개만 `Corroded=true`이고 나머지 11개는 결함 없음으로 두었다 — 근거 없는 결함을 지어내지 않기 위해서다.

### 새로 드러난 것 — 유닛 간 2m 간격

생성기가 경고를 냈다.

```
[슬라이스] 유닛 간 최소 간격 2.00m · BSN-CONC-FE-004 ↔ BSN-CONC-FE-006
           · 상호작용 상한 3m 안이라 조준이 모호할 수 있습니다.
```

index 3 `(48, 7.02, -47)`과 index 5 `(48, 7.0, -49)`다. `wallNormalDir`이 서로 반대(`0,0,-1` / `0,0,1`)라 마주 보는 벽에 붙은 것으로 보인다. `RefreshInteraction`이 3m 상한 안에서 가장 가까운 콜라이더만 고르므로 둘 다 사정거리에 들면 어느 것을 겨눈 것인지 모호해진다.

원본 자료의 좌표라 임의로 옮기지 않았다. 실제로 조준이 모호한지는 플레이로 확인해야 한다. 튜토리얼 대상(index 9)과는 무관하므로 현재 슬라이스는 막히지 않는다.

---

## 기술자 인계 씬 배선 — 완료 (2026-09-24)

한 번 보류했다가(사용자 결정 C) 12개 좌표 주입 후 복원했다. 씬에 `TechnicianDispatch` 1개가 들어갔고 `session.Dispatch`에 연결되어 있다. PlayMode 45/45 통과.

다만 `dispatch.Target=tutorialTarget`이므로 **인계는 튜토리얼 대상 1개에만 걸린다.** 나머지 11개는 점검 가능한 설비로 배치되어 있으나 세션에도 인계에도 물려 있지 않다. 아래 "다중 대상" 항목과 같은 제약이다.

---

## 다중 대상 — 완료 (2026-09-24, B안)

`.planning/2026-09-22-station-interior-build/jev-jev-inject-structure-005.result.json` 에 판정이 **둘** 있었다.

| 질문 | 판정 | 확률 |
|---|---|---|
| `session_and_terminal` | `one_each_hoisted` | 0.87 |
| `inspectable_scope` | `all_twelve_with_per_unit_state` | 0.65 |

A안(설비별 세션)은 첫 번째를 정면으로 위반하므로 제외했다. 둘을 합치면 B안 — 세션 하나가 12개의 개별 상태를 관리 — 이고, 기존 구현은 `inspectable_scope` 에서 확률 0.34 였던 `one_tutorial_target_rest_props` 쪽이었다. 즉 기록된 판정과 어긋나 있었다.

구조: `UnitState{Facility, Dispatch, Runner, Finished, RoleBoundaryViolations, MisjudgementCount, Findings, Handler}` 목록 + 활성 인덱스. `Target`·`Dispatch`·`Runner`·`Finished`·카운터는 활성 유닛을 가리키는 뷰다. `Update()` 가 **모든** 유닛의 인계를 진행시킨다 — 이것이 다중 대상의 요점이다.

2단계로 나눠 진행했다. ① 단일 대상 동작을 그대로 둔 채 구조만 바꿔 53/53 유지 확인 ② 그 위에 12개 배선과 시험 8건 추가. 덕분에 2단계 실패가 났을 때 "리팩터링이 깨뜨린 것"이 아니라 "새 시험 문제"라고 즉시 판별할 수 있었다. 최종 PlayMode 61/61, 씬 `FacilityInspectable` 12 · `TechnicianDispatch` 12 · 세션 1 · 단말 1 · `Units` 12.

영수증: `docs/CHOOGuard_Story_Plan_v5/state/evidence/2026-09-24-multi-target.json`

### ECC 리뷰가 잡은 것 (Block 권고, HIGH 4 + MEDIUM 1)

- **`OnDestroy` 가 `InteractionPerformed` 구독을 해제하지 않았다.** 익명 람다로 `+=` 만 해서 뗄 수가 없었다. 생성기가 유닛 루트와 세션 호스트를 따로 두므로 설비가 세션보다 오래 사는 것은 설계상 가능하고, 그 경우 죽은 세션의 `Advance`·`Audit`·이월 기록이 한 번 더 발화한다. `UnitState.Handler` 에 참조를 보관해 대칭 해제.
- **`Target`/`Dispatch` 대입이 조용히 무시됐다.** "공개 API 를 전부 유지했다"는 내 주장이 틀렸다 — 이름과 타입은 같지만 쓰기 의미론이 사라져, `session.Target=B` 가 아무 효과 없이 다음 `Activate` 에서 덮어써졌다. `[SerializeField] private` + 속성으로 바꿔 대입이 활성 전환이 되게 했다. **직렬화 이름이 바뀌므로 씬 재생성이 필요하다.**
- **`AuditFindings` 가 내부 리스트를 그대로 노출했다.** `AuditTerminal.LastReport.Findings` 가 세션 내부 리스트와 동일 객체라, 다른 유닛을 감사하면 이미 보관된 보고서가 바뀐다. 매번 스냅샷을 만들도록 수정.
- **내 시험이 동어반복이었다.** "인계는 활성이 아닌 유닛도 함께 진행한다"가 `dispatchA.Tick()` 을 직접 불러, 검증하려던 `TutorialSession.Update()` 를 전혀 실행하지 않았다 — `Update()` 를 지워도 통과했다. 직전 답변에서 내가 "동어반복이 아닌지 봐달라"고 지목한 바로 그 항목이었다. `[UnityTest]` 로 프레임 구동하도록 고쳤다.
- **`Bind()` 의 상호작용 클로저를 발화시키는 시험이 하나도 없었다.** 클로저가 엉뚱한 유닛을 잡도록 회귀해도 7건 전부 통과했을 것이다. `TryInteract` 로 실제 경로를 타는 시험을 추가했다.

고친 시험 2건이 처음엔 실패했는데, 그것이 곧 **진짜 경로를 타기 시작했다는 증거**였다. `FirstPersonResponder.IsPaused` 는 기본값이 `true` 이고 `CanInteract` 가 이를 가장 먼저 막는다(`SetExternalInputMode(true)` + `Resume(false)` 필요). 배치모드의 `Time.deltaTime` 은 매우 작아 짧은 소요라도 프레임 수를 예측할 수 없다(소요를 0 으로 두면 `Tick` 한 번에 한 단계씩 확정적으로 넘어간다). 이전 판은 이 둘을 만날 일이 없어서 통과했고, 그래서 아무것도 지키지 못했다.

---

## (배경) 다중 대상은 세션 구조 문제지 설비 부족이 아니다

설계 문서에 "대기 중 점검할 두 번째 소화기가 없다"고 적었던 것은 사실과 다르다(2026-09-24 정정). 씬에는 `FacilityInspectable`이 12개 있다. 실제 제약은 이렇다.

- `TutorialSession.Target`이 단수 필드이고 `Runner`도 하나다. 설비마다 독립된 절차 진행 상태가 없다.
- `FireExtinguisherSliceBuilder.Placement.TutorialTarget` 주석: "절차 세션이 물릴 대상. **정확히 하나여야 한다.**"
- 생성기 주석에 따르면 세션·판정 단말을 유닛마다 만들지 않는 것은 Jev 판정(`session_and_terminal=one_each_hoisted`, 0.87)에 따른 의도적 설계다. 유닛 루트를 지워도 살아남아야 하기 때문이다.

따라서 다중 대상화는 그 판정을 다시 검토하는 일이 된다. 접근법 후보 두 가지:

| 방식 | 내용 | 비용 |
|---|---|---|
| A. 설비별 세션 | 소화기마다 세션+인계를 하나씩. 세션 코드 무수정 | 작음. 단 판정 단말이 여러 세션을 집계해야 하고 `one_each_hoisted` 판정과 충돌 |
| B. 세션 다중 대상화 | 세션이 설비 목록과 설비별 러너를 관리, 활성 대상 전환 | 큼. `Bind`/`Observe`/`Advance`/`Audit`/`Restart` 전부 영향 |

---

## 시험이 실제 배포 자료를 읽지 않던 공백 (2026-09-23, 해소)

기존 PlayMode 시험은 전부 자체 축약본 JSON 상수를 썼고 `Assets/ChooGuard/Art/Procedures/fire-extinguisher-monthly.json`을 한 번도 읽지 않았다. 그래서 절차 자료를 v2로 올렸을 때 `ProcedureRunner.Load()`의 `version!=1` 검사에 걸려 런타임에 튜토리얼이 통째로 죽는 상태였는데도 시험 32건이 전부 통과했다.

`배포된_절차_자료가_실제로_로드된다` 시험을 추가해 메웠다. 파일이 없으면 `Assert.Ignore`가 아니라 **실패**하게 했다 — 스킵되는 시험은 경로가 바뀌면 조용히 사라진다.

---

## ECC 리뷰가 잡은 것 (2026-09-23)

- **HIGH** `TechnicianDispatch.Tick`의 시간 이월이 죽은 코드였다. `SecondsInStage-=need` 직후 `Enter()`가 `SecondsInStage=0`으로 덮어써서 초과분이 버려졌다. 주석은 "이월된다"고 적혀 있었으므로 **주석이 거짓이었다.** `Enter()` 이후에 초과분을 다시 넣도록 순서를 바꿔 수정.
- **MEDIUM** 판본 검사가 어휘를 제한하지 않아 `version: 1` 자료가 v2 전용 효과·사실을 써도 통과했다. `FactsAddedInV2`/`EffectsAddedInV2`로 분리하고 거부 메시지에 판본을 포함시켰다.
- 리뷰는 내 시험의 맹점도 짚었다 — 소요 시간과 정확히 일치하는 값(2,5,3)이나 극단값(999)만 써서 "필요치보다 살짝 많은 값" 조합이 없었고, 그래서 HIGH 결함을 잡지 못했다. 겨냥한 시험 2건을 추가했다.
