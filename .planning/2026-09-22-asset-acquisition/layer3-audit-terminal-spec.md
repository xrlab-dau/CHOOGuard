# Layer 3 구현 명세 — 판정 단말(감사 디브리프)

전제: 배치 `world_terminal_screenspace`, 게이트 `present_but_refuses` (Jev 006). 참조해상도 1440x900.

## 1. 파일 목록

**신규 2개**
| 파일 | 이유 |
|---|---|
| `Assets/ChooGuard/App/Fps/Work/AuditTerminal.cs` | E 계약·게이트·열림상태. `FacilityInspectable` 과 같은 Work/ 에 두어 모드 중립을 유지한다 |
| `Assets/ChooGuard/App/Fps/Work/AuditTerminalView.cs` | 스크린스페이스 캔버스 생성. `FirstPersonInteractionHud.cs:6` 이 HUD 에 디브리프를 금지했고, Responder(상태)/Hud(뷰) 분할을 그대로 따른다 |

**수정 1개**
| 파일 | 이유 |
|---|---|
| `Assets/ChooGuard/App/Fps/Tutorial/TutorialSession.cs` | `public AuditTerminal AuditTerminal;` 필드 + `Bind()`(64-70행)에서 델리게이트 2개 결선. 그 외 한 줄도 건드리지 않는다 |

**새로 만들지 않는 것 (기존 바퀴 그대로)**
`FpsInteractable`(E 경로·Prompt·UnavailableMessage) · `FirstPersonResponder`(레이캐스트·E 엣지·Esc·`CurrentPrompt`:128행·`ShowFeedback`:136행) · `TutorialSession.Audit()/AuditFindings`(자료 완성) · `NotoSansCJKkr SDF.asset`(존재 확인) · **ScrollRect·VerticalLayoutGroup·ContentSizeFitter 전부 미사용** (후자 2종은 저장소 0건, 유일한 ScrollRect 선례 `MvpWorkspace.cs:248-251` 은 content sizeDelta 고정이라 가변 길이에 이미 틀렸다).

## 2. 화면 구성 (Label(name, offset, size, fontSize) 관례)

캔버스: `ScreenSpaceOverlay`, `ScaleWithScreenSize`, ref `(1440,900)`, match `.5`, **`sortingOrder=10`** (HUD 캔버스 0 위로).
패널 배경: Image, anchor(.5,.5), offset `(0,0)`, size `(1000,620)` → y `+310..-310`, 색 `(0.04,0.05,0.06,0.94)`.
바깥 220px/140px 여백으로 월드가 계속 보인다(부분 화면 — Viscera/My Summer Car 계열).

| 요소 | offset | size | fontSize | 정렬 |
|---|---|---|---|---|
| 제목 `"점검 감사 단말"` | `(0,276)` | `(960,40)` | 26 | Center |
| 부제 `"{장소} · 지적 {N}건"` | `(0,238)` | `(960,32)` | 20 | Center |
| 구분선 (Image) | `(0,212)` | `(940,2)` | — | — |
| 본문 (지적 열거) | `(0,-22)` | `(940,452)` | 19 | **TopLeft** |
| 안내 `"E · 닫기   Esc · 정지"` | `(0,-278)` | `(960,32)` | 17 | Center |

본문은 **TMP_Text 1개**에 `string.Join("\n", findings)`. 가변 길이는 TMP 내장 `enableAutoSizing=true, fontSizeMin=14, fontSizeMax=19` 로 흡수한다 — 새 레이아웃 기계를 들이지 않는 유일한 방법. 최악(11건 전부 2줄 = 22줄)도 14px 에서 약 447px 로 452px 안에 들어간다(계산치).
HUD 의 prompt(0,-155)·feedback(0,-210)·중앙점은 패널 뒤로 가려진다 — 그래서 닫기 안내를 패널이 직접 든다.
`Label` 헬퍼는 HUD 30-33행을 그대로 복사하되 `TextAlignmentOptions` 인자 1개만 추가한다.

## 3. 위반 0건일 때

**같은 패널, 같은 크롬.** 별도 화면·메달·축하 연출 없음(My Summer Car 영수증 선례).
- 부제: `"{장소} · 지적 0건 · 절차 적합"`
- 본문: 열거 대신 한 줄 — `"지적 사항 없음 — 제22조·제23조 절차를 모두 충족했습니다."`, 이때만 `alignment=Center`(수직 중앙).
빈 패널을 그리지 않는다. "즉시 야단치지 않는다"(TutorialSession.cs:141)의 반대편도 과장하지 않는다.

## 4. 열고 닫는 조작 — 새 입력 배선 0

E 는 `FirstPersonResponder.cs:105` 에서 엣지 트리거이고 `TryInteract()`→`InteractionPerformed` 로 이어진다. 그 **한 경로를 토글**로 쓴다.

- **열기**: 단말을 보며 E. `Finished` 전이면 `CanInteract` 가 false 를 돌려주고 사유가 128행(CurrentPrompt)·136행(ShowFeedback) 두 곳에 이미 표시된다.
- **닫기 ①**: 다시 E (엣지라 누르고 있어도 1회). `Prompt` 필드를 열림 상태에 따라 `"감사 결과 확인"↔"감사 결과 닫기"` 로 바꾼다 — `TutorialSession.RefreshPrompt()`(187행)가 쓰는 것과 같은 수단.
- **닫기 ②**: 시선이 단말을 벗어나면 자동 닫힘(`Responder.CurrentTargetCollider` 가 자기 콜라이더가 아니면).
- **가림 ③**: `Responder.IsPaused` 동안 캔버스 비활성(Esc 정지 오버레이와 겹치지 않게). 정지 중엔 `FpsInteractable.cs:19` 때문에 E 가 막히므로 **닫지 않고 숨기고**, 재개 시 그대로 돌아온다. 못 닫는 상태를 만들지 않는다.
- **강제 닫힘 ④**: 열린 채 `GateReason()` 이 다시 사유를 내면(`Restart`/`Rewind` 가 `Finished=false`) 즉시 닫는다.

Esc 는 건드리지 않는다(69행 Pause 점유 그대로).

## 5. 세션 결합 — 직접 참조 아님, **Func 델리게이트 역전**

`AuditTerminal` 이 노출하는 것은 두 개뿐:
```
public Func<string> GateReason;                       // null=통과, 문자열=거부 사유
public Func<IReadOnlyList<string>> FindingsSource;    // 지적 열거
```
`CanInteract` 오버라이드는 `FacilityInspectable.cs:80-86` 을 그대로 복제한다(3줄 — 기반 클래스로 올리면 FacilityInspectable 이 게이트를 2회 호출하게 되어 기존 계약이 깨진다).

근거 3가지:
1. **선례**: `FacilityInspectable.cs:75-78` 주석이 "설비는 세션 타입을 모른다"를 이미 확정했다.
2. **경계 리트머스**: `TutorialSession.cs:9` — Tutorial 네임스페이스와 Emergency 는 서로 참조하지 않는다. 단말이 `TutorialSession` 을 직접 들면 `Fps.Work` → `Fps.Tutorial` 의존이 생겨 비상 모드가 같은 단말을 영영 못 쓴다. 제품 정의상 두 모드는 별개다.
3. **결선 위치가 이미 있다**: `TutorialSession.Bind()`(64-70행)가 `Target.GateReason` 을 꽂는 바로 그 자리에 두 줄을 더한다. 새 수명주기 훅 불필요.

컴파일 경계는 없다(둘 다 `ChooGuard.App` 단일 asmdef) — 이 규율은 **설계 규율이지 컴파일러가 막아주지 않는다**.

## 6. 시험 (PlayMode, `Assets/ChooGuard/Tests/PlayMode/AuditTerminalTests.cs`)

`ChooGuard.PlayModeTests` asmdef 는 `ChooGuard.App`·`Unity.TextMeshPro`·`UnityEngine.UI` 를 이미 참조한다. `FpsProcedureTests.cs` 의 SetUp(축약 절차 JSON + `SetExternalInputMode(true)`)을 그대로 빌린다. OS 입력 합성 금지 — `responder.TryInteract()`(133행, public)로 E 경로를 탄다.

1. `점검_종료_전에는_단말이_거부하고_화면이_열리지_않는다` — `Finished=false` 에서 `TryInteract()==false`, 캔버스 비활성, `responder.CurrentPrompt` 가 게이트 사유와 같다.
2. `점검_종료_후_E_한_번에_지적_줄이_모두_보인다` — `close-inspection` 까지 진행 후 E. 본문 텍스트가 `session.AuditFindings` 의 **모든 항목을 포함**하고 개행 수가 `Count-1` 이다.
3. `지적_0건이면_목록_대신_적합_문구를_그린다` — 무결 수행(`PendingVerdict` 정답, 인계·부착 완료). 본문 == 적합 문구이고 `"미완료"` 를 포함하지 않으며, 부제가 `"0건"` 을 포함한다.
4. `열린_상태에서_E_를_다시_누르면_닫힌다` — E·E 후 캔버스 비활성, `Prompt` 가 `확인↔닫기` 로 왕복한다.
5. `Esc_정지_중에는_단말_화면이_숨고_재개하면_돌아온다` — 연 뒤 `responder.Pause()` → 캔버스 비활성·열림상태 유지, `Resume()` → 다시 활성.

## honest_limits

- **`l3-oss.md` 가 존재하지 않는다.** 스크래치패드에 없어 읽지 못했다 — OSS 조사 결과가 이 명세에 반영되지 않았다. 다른 OSS 선례가 이 판단을 뒤집을 수 있다.
- **Unity 를 실행하지 않았다.** 위 수치·좌표를 렌더링으로 확인한 바 없다. 컴파일·시험 실행 전부 미수행.
- **TMP 줄 높이는 계산치다.** NotoSansCJKkr SDF 의 실제 face lineHeight/pointSize 비(1.45 가정)를 자산에서 읽지 않았다. 14px 에서 447/452px 는 여유가 5px뿐이라 **실측에서 넘칠 수 있다** — 넘치면 본문 size 를 `(940,480)`, 안내를 `(0,-286)` 으로 내리는 것이 1차 수선이다.
- **`enableAutoSizing` 을 이 저장소에서 쓴 선례가 없다** (grep 미수행). 프로젝트 TMP 설정과의 상호작용 미확인.
- **한국어 한 줄의 실제 픽셀 폭 미측정.** "940px/19px ≈ 49자"는 글리프 폭 = fontSize 가정이며 CJK 는 대개 맞지만 검증하지 않았다. 줄바꿈 횟수 추정이 틀릴 수 있다.
- **게임 조사 6종 중 PowerWash Simulator 5문 전부 `unverified`**, Hardspace·TSW 의 배치·정렬도 대부분 `unverified`. 화면 배치 수치는 선례가 아니라 이 저장소의 HUD 관례에서 나온 것이다.
- **`ProcedureRunner.Unmet` 이 실제로 최대 몇 단계를 돌려주는지** 확인하지 않았다(브리프의 "0~8줄"을 그대로 받았다). 11줄 상한이 틀리면 자동축소 여유 계산도 틀린다.
- **역무실 씬에 단말 오브젝트·콜라이더가 존재하는지 확인하지 않았다.** 씬 배치와 프리팹은 이 명세 범위 밖이다.
- **`RoleBoundaryViolations` 를 늘리는 `TryFieldRepair` 의 호출자**를 찾지 않았다 — 4번째 지적 유형이 실제로 발생 가능한지 미확인.
