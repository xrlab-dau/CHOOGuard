# CHOOGuard Unity Native UI/UX v2
## 웹 화면이 아니라, 운영 시뮬레이터의 Game View를 설계한다

**문서:** UX-CHOOGUARD-NATIVE-002 / 2026-09-19  
**대상:** PRD v11의 Unity PC 운영 실험 도구. 기존 91개 제품 요구를 대체하거나 종료하지 않는다.  
**변경:** HTML 기반 전달물을 Unity native 설계·입력·계층·검수 계약으로 교정.  
**범위:** 네이티브 UX 설계 명세 + uGUI/TMP 레이아웃 생성기 소스. Unity 컴파일·렌더·Player 실행은 NOT_RUN.  
**현재 지시:** 이 문서는 UI 플랫폼·표현·입력 기준에서 UX v1보다 우선한다. v1의 사용자 과업과 근거는 유지하지만 HTML/DOM/브라우저 반응형·웹 테스트는 구현 정본이 아니다.

---

## 1. 교정 결정

제품은 브라우저, Electron, WebView, WebGL 웹앱이 아니다. 기존 Unity PC 애플리케이션의 native runtime UI다. HTML을 Texture로 띄우거나 웹 대시보드를 임베딩하지 않는다.

**주 런타임 UI는 uGUI(Canvas) + TextMeshPro로 선택한다.** GameObject/Prefab, RectTransform, CanvasGroup, EventSystem, GraphicRaycaster 기준으로 설계한다. 개발자용 EditorWindow는 별도 제작 도구이고 사용자 런타임이 아니다. UI Toolkit도 Unity의 native UI지만, 이번 교정에서는 프레임워크를 두 개 섞지 않고 uGUI 계층으로 인계를 통일한다. 이 선택은 'uGUI가 모든 용도에서 우월하다'는 주장이 아니라 게임 HUD·월드 표시·프리팹 조립을 일관되게 설계하기 위한 프로젝트 결정이다. [U01][U02]

새 입력의 목표는 Input System이다. **현재 develop manifest/lock에서 com.unity.ugui 및 com.unity.inputsystem을 찾지 못했다.** 기존 com.unity.modules.ui를 완전한 uGUI/TMP 패키지로 오인하지 않는다. 패키지 도입은 실제 작업 브랜치에서 호환성을 확인한 후 수행하며 이번 문서·소스 제공은 manifest를 수정하지 않는다. 기존 입력이 있으면 어댑터를 쓰고 전역 Active Input Handling을 자동 변경하지 않는다.

### 유지 / 폐기

| 유지 | 교정·폐기 |
|---|---|
| 작성 담당자 중심, 팀·업무 선택, 근거와 비교 | 웹 페이지 이동·DOM이 실제 게임 UI라는 가정 |
| 두 모드: TUTORIAL / RANDOM_OPERATIONS_LAB | 모바일 웹 390px 레이아웃을 PC Game View의 수용 조건으로 사용하는 것 |
| 실제 물리·운영 코어와 검증 자격 | HTML 목업 결과로 Unity 프레임·입력·접근성 통과 주장 |
| S01–S12의 사용자 목적과 EP00–EP11 | 브라우저 앱과 Unity 양쪽을 모두 필수 구현하는 이중 개발 |
| 무료 에셋 우선, 실제 공간 근거 | 웹 화면을 게임 내부 RenderTexture에 올리는 우회 구현 |

## 2. Game View의 공간 구성

**메뉴를 제외한 주 화면에서는 WorldCamera가 실제 3D 공간을 직접 렌더링한다.** HUD는 그 위에 놓는 Screen Space Overlay다. 월드 전체를 큰 웹 카드 내부의 그림처럼 취급하지 않는다. 미니맵·비교 스냅샷만 필요시 별도 Camera/RenderTexture를 사용한다. [U02]

### 운영 HUD / S05 기본 배치 — 설계값, 실제 렌더 검증 전

- 기준 1920×1080. 상단 72–84 UI 단위에 현장·모드·운영안·실행 상태·계산 시각과 핵심 제어를 둔다.
- 좌측 약 300–330 단위는 접을 수 있는 기관·팀/업무 목록이다. 항상 거대한 내비게이션을 펼쳐 지도 폭을 소모하지 않는다.
- 우측 약 360–390 단위는 선택한 업무·대상의 인스펙터다. 선택하지 않으면 불필요한 상세를 접는다.
- 하단 좌측에 미니맵, 중앙에 선택 묶음과 사용 가능한 요청, 우측에 최근 사건 요약을 둔다.
- 전체 타임라인은 하단 접이식 트레이로 펼친다. 작업 중에는 요약 레일, 분석 중에는 확대 트레이를 쓴다.
- 중앙의 빈 HUD 영역은 raycast를 받지 않는다. 패널·버튼·대화상자만 필요한 입력을 가로챈다.
- 월드 라벨은 소속·팀명·작업 상태를 짧게 보여준다. 상세 근거는 인스펙터로 이동한다.
- 기관별 색과 상태색의 의미를 혼합하지 않는다. 상태는 텍스트·아이콘·선 스타일로도 구별한다.

**기본 UX는 '지도에서 보며 조작'이다.** 대본 편집과 상세 비교에서는 넓은 작업 오버레이를 열 수 있다. 그것은 게임 안의 편집 작업이고 별도 웹 페이지가 아니다.

## 3. Unity 계층과 수명

다음은 최종 구현의 권장 계층이다. 제공 생성기의 단일 Preview Canvas는 구조 확인용이며 아래 전체 분할과 풀링을 구현한 것은 아니다.

```text
ApplicationRoot                       # 기존 부트스트랩 재사용
├─ SimulationSessionBridge            # 기존 운영 코어와 연결; UI가 소유하지 않음
├─ UIStateStore                        # 선택·열린 패널·사용자 환경만 보관
├─ InputContextRouter                  # 입력 소유권·focus·modal 관리
├─ EventSystem                         # 활성 입력 모듈 한 개
├─ WorldCameraRig                      # 기존 카메라와 연결
├─ WorldAnchorRegistry                 # EntityId ↔ Transform, pooled markers
└─ NativeUIRoot
   ├─ HUDCanvas          order 100     # Screen Space Overlay
   │  ├─ TopStatusBar
   │  ├─ AgencyRosterDrawer
   │  ├─ TaskInspector
   │  ├─ SelectionCommandDock
   │  ├─ MinimapPanel
   │  └─ EventTimelineTray
   ├─ MarkerCanvas       order 110     # 화면 투영 라벨; 장식은 raycast off
   ├─ ToolOverlayCanvas  order 200     # 비교·대본·설정
   ├─ ModalCanvas        order 300     # 승인 전 미리보기·분기·출력
   └─ HelpCanvas         order 400     # tooltip / tutorial callout; 비차단 기본
```

Canvas 분리는 표시 갱신 빈도와 입력 책임에 따른다. 모든 리스트 행마다 독립 Canvas를 추가하지 않는다. UGUI Canvas의 렌더 모드와 계층 순서를 이용하되 실제 배칭·리빌드 비용을 Unity Profiler로 검수한다. [U02]

UI를 닫거나 씬의 렌더링 구역을 내리는 것으로 운영 세션을 종료하지 않는다. 카메라·선택·텍스트 크기 변경은 물리 시간·자원·업무를 바꾸지 않는다. DontDestroyOnLoad를 무조건 중복 사용해 EventSystem과 Canvas를 여러 개 만들지 않는다.

## 4. S01–S12를 native surface로 매핑

| ID | native 형태 | 진입 / 조작 / 복귀 | 보존할 상태 |
|---|---|---|---|
| S01 | 런처 메뉴 Canvas/Prefab | 최근 현장 열기, 두 모드, 설정; URL 내비게이션 없음 | 최근 작업, 로컬 환경 |
| S02 | 현장 확인 Prefab | 자격·버전·자료 공백 확인 후 설정 | 선택 SiteBundleVersion |
| S03 | 상황 설정 Prefab | 검토된 조건 선택, 입력 검증, 새 실행 요청 | DraftScenario, 규칙·모델 버전 |
| S04 | HUD tutorial callout | 관련 객체 강조, 안내 접기·다시 보기 | 학습 진도; 운영 조건 변경 금지 |
| S05 | 실제 3D 뷰 + HUD | 선택·드래그·명령·카메라·상태 감시 | SelectionSet, ViewMode, SessionId |
| S06 | 우측 inspector / pinned evidence drawer | 대기 원인·관련 이벤트·원문 확인 | 대상 ID, 선택한 원인, 스크롤 |
| S07 | Command Preview modal | 적격/부적격 대상·예상 영향 확인; 요청 또는 취소 | IntentId, stateVersion, 대상별 결과 |
| S08 | Branch modal | 체크포인트 자격·분기 이름·변경 범위 | 원본 A, 원 스냅샷, 분기 요청 ID |
| S09 | 넓은 비교 작업 overlay | 공정성 검사·타임라인·결과 차이; 선택 이유 | A/B references, viewport, metric basis |
| S10 | native authoring overlay | TMP 입력·문장 근거·의미 diff·검수 | DraftRevision, dirty state, cursor |
| S11 | Export modal | 초안/기관 수용 구분·파일 형식·오류 처리 | Qualification, template version |
| S12 | 설정 overlay | 키·글자·고대비·motion·입력 도움 | 사용자 설정, 원 실행 유지 |

S04와 S06는 최종적으로 기본 월드 조작을 불필요하게 막지 않는 레이어다. 제공 구조 프리뷰에서는 화면 탐색을 단순하게 하려고 별도 창으로 열며, 이것을 최종 입력·레이아웃의 완성 상태라고 취급하지 않는다.

### 사용자 흐름

**운영:** S01 → S02 → S03 → S05 → S06 → S08 → S07 → S05 → S09 → S10 → S11.

**교육:** S01 → S02/03 → S05 + S04 → 목표·조건 확인 → 직접 조작 → 복기. 모드는 새 프레임워크나 별도 물리 코어가 아니다.

모달은 원 화면·카메라·선택을 유지하고 닫을 때 이전 focus로 복귀한다. 실험 일시정지는 UI 전환과 별개이며 코어의 pause acknowledgement를 받은 뒤 표시한다. UI에서 Time.timeScale만 0으로 바꾸는 것을 다중 solver 정지로 취급하지 않는다.

## 5. RTS 입력 계약

### 입력 우선순위

`활성 모달 → 텍스트/IME 편집 → HUD·스크롤·UI 드래그 → 월드 선택/명령 → 카메라`.

순서는 개념상 소유권이다. 다른 컨텍스트가 소비한 이벤트를 월드나 카메라에 다시 전달하지 않는다. 입력 라우터에서 원본 pointer-down 시점의 소유자를 기록하고 up/cancel까지 유지한다. EventSystem UI hit와 게임 입력의 중복 처리는 명시적으로 차단해야 한다. Unity 입력 문서도 UI와 게임 입력 간 모호성을 별도로 다룬다. [U03]

| 입력 제안 | 월드에서의 의미 | UI·focus 상태에서의 처리 |
|---|---|---|
| 좌클릭 | 객체 선택 | 컨트롤 작동; 뒤의 팀 선택 금지 |
| Shift+좌클릭 | 선택 묶음에 추가/제거 | 텍스트 선택 또는 UI에 우선권 |
| 좌드래그 | 현재 층·가시성·권한 조건의 묶음 선택 | UI에서 시작하면 월드 드래그로 전환 금지 |
| 우클릭 | 해당 대상의 문맥 명령 후보 표시 | UI 내 context 또는 소비; 뒤편 이동 명령 금지 |
| Shift+명령 | 명령 큐에 요청; 선행조건은 그대로 | UI 텍스트/IME 중 비활성 |
| 휠 | 월드 줌 | ScrollRect 위에서는 해당 패널 스크롤만 |
| 중버튼 드래그 / Q,E | 카메라 회전 | UI 작업·모달·텍스트 입력 중 차단 |
| WASD / 방향키 | 카메라 이동 | 입력칸·IME 또는 UI 탐색 시 차단 |
| F | 선택 대상 초점 | 입력칸과 모달에서는 게임 동작 금지 |
| Space | 코어에 정지/재개 요청 | TMP 입력 중 공백; UI Submit 중복 매핑 금지 |
| Escape | 문맥 명령 취소 → 모달 닫기 → 선택 해제/설정 | IME 조합 취소와 텍스트 편집 종료를 먼저 처리 |
| Ctrl+1..9 | 선택 그룹 저장 | 단축키 재배정 가능; 입력칸 우선 |
| 1..9 | 선택 그룹 불러오기 | 입력칸 숫자와 중복하지 않음 |
| Tab / Shift+Tab | 명시적 UI focus 순서 | focus가 모달 밖으로 이탈하지 않음 |

이 기본키는 제품 설계 제안이며 재배정 가능해야 한다. 실험 코어 동작을 자동 실행시키는 단축키로 확정한 것이 아니다. UI, Operations, Camera, Authoring 액션맵을 별도 관리하고, 기존 입력 체계의 승인된 바인딩과 충돌을 검사한다. [U03]

### 카메라·선택의 정확성

포인터 좌표는 Screen 좌표이며, UI는 RectTransform 로컬 좌표로 변환한다. 월드 선택은 지정 Camera의 ScreenPointToRay와 명시적 layer mask를 사용한다. screen y 반전·DPI·camera pixelRect·letterboxing·현재 층을 고려한다. UI hit를 무시한 월드 raycast는 금지한다.

숨긴 천장은 renderer만 숨긴다. 대응 이동을 결정하는 collision/nav/solver 개구는 유지한다. 선택 raycast 전용 계층과 이동 collision 계층을 분리해 천장 숨김 뒤의 선택 가능성을 관리한다. 가려진 객체를 드래그에 포함할지는 현재 층/분석 시점 기준을 명시하고, 포함 개수를 미리 표시한다.

## 6. 월드 라벨·미니맵·카메라

팀·차량 위의 UI는 WorldAnchorRegistry의 EntityId로 실제 Transform을 조회해 투영한다. 카메라 뒤의 점, 다른 층, 비공개 기관 정보, occlusion 상태를 처리한다. 수백 개 라벨은 풀링·거리별 정보 단순화·선택 우선 표시로 제한한다. 선택 링과 경로는 게임 렌더링, 상세 정보는 native HUD로 분리한다.

미니맵은 전용 Camera + RenderTexture + RawImage로 구성하거나 동일 월드 데이터의 평면 투영을 쓴다. 클릭 위치는 미니맵 좌표계를 거쳐 월드 카메라 목표로 변환한다. 건물별 층·진입 경계·북쪽 방향을 명시한다. 미니맵은 이미지 한 장을 붙인 가짜 현재 상태가 아니다.

비교 오버레이를 열 때 월드 카메라와 선택을 복원 가능한 UI 상태로 보관한다. 타임라인의 과거를 탐색하는 행위가 실행 중인 기관에게 미래 지식을 부여하지 않는다.

## 7. 타이포그래피·스케일·native 접근성

uGUI의 Anchors와 CanvasScaler는 해상도에 맞춘 배치와 비례 스케일의 기반이다. 이 기능만으로 큰 글자·읽기성·레이아웃이 자동 검증되는 것은 아니다. [U04]

- 설계 기준 1920×1080, Match Width Or Height 초기값 0.5. 1280×720, 1366×768, 2560×1440, ultrawide와 창 크기 변경을 별도 시험한다.
- 글자 100/125/150/200%는 사용자 설정이며 카메라 줌이나 OS DPI와 분리한다. 자동 글자 축소로 큰 글자 설정을 무력화하지 않는다.
- 좁은 Game View에서는 좌측 roster를 drawer로 바꾸고 선택·명령을 유지한다. 비교·대본은 넓은 overlay로 전환한다. 세 개의 고정 사이드바를 계속 눌러 넣지 않는다.
- TextMeshPro의 한글 font asset/fallback을 프로젝트에서 공급한다. IME 조합·확정·취소·Enter·Esc·클립보드·긴 문장·커서 복귀를 Windows Player 등 실제 배포 환경에서 시험한다. 폰트 파일은 이 패키지에 포함하지 않는다.
- 버튼은 focus·hover·pressed·disabled를 색 이외에도 표현한다. 비활성 사유는 별도 읽을 수 있어야 한다.
- hover에만 중요한 근거를 숨기지 않는다. 패널 기반 명령과 키보드 경로로 월드 정밀 클릭을 대체한다.
- DOM/ARIA 검사 결과를 Unity 스크린리더 지원으로 복사하지 않는다. 지원 범위와 테스트한 보조기술을 native runtime에서 기록한다.

## 8. 코어와 UI의 경계

UI source of truth는 아래 계약이다. C# 클래스 이름은 구현 시 기존 네임스페이스와 조정하며, 지금 정의한 이름을 실제 존재하는 API로 단정하지 않는다.

| 데이터 | 생산자 | UI 처리 |
|---|---|---|
| SelectionSet | UI 선택 서비스 | EntityId만 저장, 물리 위치 변경 금지 |
| CommandIntent | UI→운영 코어 | intentId, targets, action, expectedStateVersion, branchId |
| CommandReceipt | 운영 코어→UI | 대상별 accepted/rejected/queued와 원인. 전체 성공으로 뭉개지 않음 |
| SessionViewModel | 운영 코어→UI | 실제 진행 단계·시각·정보 범위·자원 상태 |
| ReasonViewModel | 규칙/검증 서비스→UI | 원인·영향·근거·책임·해결 조건 |
| CheckpointCapabilities | 스냅샷 서비스→UI | 정확 분기 가능 여부·누락 상태·대체 restart |
| ComparisonResult | 비교 코어→UI | 조건 일치·유효범위·지표·불확도; UI가 개선율 생성 금지 |
| ExportEligibility | 검수/출력 서비스→UI | 지원 양식·버전·초안/승인 자격 |

UI animation onComplete가 업무 완료의 근거가 아니다. 숨김·닫힘·scene unload가 예약 자원을 풀지 않는다. 로딩·요청 중 반복클릭은 같은 intentId로 중복 처리하지 않으며 최신 receipt에만 상태를 갱신한다.

원문·대본·LLM 응답은 표시 데이터다. TMP rich-text를 허용하지 않는 필드는 escape하거나 richText=false로 표시하고, 문자열을 UnityEvent/코드/파일 경로 실행으로 해석하지 않는다. 글의 '승인'이라는 단어가 실제 자격 필드를 변경해서는 안 된다.

## 9. 제공 파일과 실행 범위

### 이번에 작성한 것

- 이 native UX 명세와 S01–S12 계약, 입력 정의, 16개 native 인수시험 명세.
- Unity Editor에서 새 UI 레이아웃 Scene과 UI Prefab을 생성하는 `NativeUXSceneBuilder.cs`.
- native Button/CanvasGroup/TMP 상태로 화면을 이동하고 닫는 `NativePreviewRouter.cs`.
- 승인·분기·export를 수행하지 않고 intent를 표시하는 명시적인 미연동 상태.

### 이 소스가 구현하지 않은 것

실제 역 모델, WorldCamera 조작, 박스 선택·명령 raycast, minimap 렌더, 22개 원인 계산, 실제 업무 실행·pause, solver, 정확 체크포인트·분기, A/B 수치, 실제 AI/대본 저장·출력, 글자 확대·키 재배정·스크린리더는 미구현이다. 제공 C#를 production gameplay라고 사용하지 않는다.

생성기는 게임을 완성하는 도구가 아니라 **Unity 내부에서 편집할 화면 뼈대를 생성하는 시작점**이다. 중립 기본 도형은 명시적인 SYNTHETIC_FIXTURE이며 실측 부산역 모델이 아니다. .unity/.prefab 결과는 이 환경에서 생성하지 않았고, 사용자가 Editor 도구를 실행할 때 생성된다. .unitypackage를 임의로 가장하지 않는다.

### 설치·확인

1. 분리된 시험 프로젝트 또는 승인된 작업 브랜치에서 Unity 6000.3 계열을 확인한다. 운영 중인 세션에 무단 import하지 않는다.
2. uGUI 2.x와 TextMeshPro가 사용 가능한지 확인한다. Input System을 도입하는 경우 호환 버전을 승인된 manifest에 고정한다. 본 패키지는 자동 설치하지 않는다.
3. 프로젝트가 사용할 한글 TMP Font Asset을 준비한다. 생성기는 선택된 asset의 한글 글리프를 확인한다. 기본 영문 폰트로 성공처럼 진행하지 않는다.
4. `unity/Assets/CHOOGuardUXNativeV2`를 Assets 아래에 복사한다. 컴파일 오류가 있으면 기존 코드를 손대기 전에 의존성·namespace를 점검한다.
5. `Tools > CHOOGuard > UX > Create Native Preview`를 열고 폰트를 지정한다.
6. 생성 버튼을 누르면 현재 수정 씬의 저장 여부를 먼저 묻고, 별도 고유 출력 폴더에 새 Scene/Prefab을 만든다. 기존 씬·프리팹을 덮어쓰지 않는다.
7. Game View에서 1920×1080을 선택하고 Play한다. 화면 전환·닫기·메모 입력을 검토한다. 버튼의 intent 표시는 코어 성공이 아니다.
8. 최종 native 입력·접근성·성능은 다음 인수시험을 실제 Editor/Player에서 진행한다.

## 10. Unity 수용 기준

`contracts/unity-acceptance.json`의 모든 시험은 NOT_RUN이다. 최소한 UI↔월드 click-through, drag 소유권, context menu, scroll routing, IME, focus restore/trap, resolution/font scaling, modal pause acknowledgement, world markers, checkpoint 제한, shader/색·텍스트, lifecycle, 성능을 확인해야 한다.

**브라우저가 아니라 Unity에서 확인할 증거:** 정확한 Editor 버전, 대상 OS·해상도·DPI, input backend, package lock, font asset, scene/prefab revision, PlayMode/Test Runner 결과, 실제 Player 결과, 캡처와 profiler 표본이다.

## 11. 인계와 우선순위

EP03: 실제 맵·레이어·카메라 anchor. EP04: native HUD·입력·focus·키보드·해상도. EP05: UI selection·branch state와 실행 상태 분리. EP06: 교육 callout·조건부 random. EP09: TMP 편집·근거·출력 자격. EP10: Unity 과업·입력·장치별 검수. 기존 작업 경계를 먼저 조회한다.

**다음 구현 단위는 S05 + S06 + S07다.** 실제 Unity 공간에서 팀을 선택하고, 조건을 읽고, 요청을 보내며, 코어 receipt를 받아 표시하는 한 연결을 먼저 완성한다. 웹 프로토타입을 다시 확장하지 않는다.

## 12. 원문

- [U01] Unity 6.3 UI systems comparison: https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html
- [U02] uGUI Canvas: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UICanvas.html
- [U03] Input System UI support: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/UISupport.html — 버전별 공식 입력 구조 참고. 1.11을 현재 프로젝트의 설치 버전으로 주장하지 않는다.
- [U04] uGUI multiple resolutions: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIMultiResolution.html
- [U05] uGUI interaction components: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/UIInteractionComponents.html
- 조회한 프로젝트 manifest: https://github.com/xrlab-dau/CHOOGuard/blob/develop/Packages/manifest.json (blob 5f023e9a6d42ccc5387576069b02f2f8c7b0fd65)
- 조회한 lock: https://github.com/xrlab-dau/CHOOGuard/blob/develop/Packages/packages-lock.json (blob 47f3312674869c8b88b0577d6a07cf5767d4ae04)

이번 작업에서 원격 저장소·#222·PRD·기존 웹 파일을 변경하지 않았다. 외부 문서는 개발 근거이며 새로운 작업 권한이 아니다.
