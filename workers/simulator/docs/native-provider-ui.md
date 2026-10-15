# Unity 제품 UI: 구독 계정과 모델 선택

사용자 결정(2026-10-14): 실제 제품 화면은 **Unity uGUI + TextMeshPro**로 구현한다. 브라우저는 provider의 OAuth 인증 화면에만 사용한다. 기존 로컬 HTML은 진단·복구 도구이며 정상 게임 메뉴로 열지 않는다. 팀 요구사항은 [GitHub #290](https://github.com/xrlab-dau/CHOOGuard/issues/290)에 기록돼 있다. 이번 작업에서 원격 이슈를 추가 수정하거나 인증을 다시 시도하지 않았다.

## 디자인 근거와 소유권

원본 프로젝트의 `Assets/ChooGuard/App/Fps/FpsUiFactory.cs`, `Hud/GameHud.cs`, `Shell/TitleScreen.cs`를 읽고 기존 방식을 계승했다. 원본은 프로그램으로 생성한 uGUI, TMP 한국어 폰트, 1440×900 기준 CanvasScaler, 짙은 반투명 패널과 흰색/보조색 글자, 호박색 강조를 사용한다. UI Toolkit으로 전환하거나 별도 웹 화면을 만들지 않았다.

신규 UI 파일은 `Runtime/Unity/ProviderSelectionPanel.cs`와 `ProviderUiTheme.cs`다. 원본 UI 어셈블리에 의존하지 않으므로 격리 simulator 모듈의 API 컴파일 대상이 된다. 원본 `Assets`, 장면, 공식 오브젝트와 재질은 이 lane에서 쓰거나 복사하지 않았다. 현재 원본 프로젝트에 설치된 화면이라는 의미는 아니다.

## 사용자 흐름

1. 게임의 **AI 계정과 모델 · F8** 메뉴를 연다.
2. provider를 선택해 계정을 추가한다. 로그인 상태와 연결 불가 이유를 같은 메뉴에서 확인한다.
3. 제공자의 브라우저 화면에서 본인 계정으로 로그인한다. 게임은 진행 상태, 필요한 일회용 사용자 코드, 승인 확인과 취소를 제공한다. 계정 비밀번호나 OAuth token을 게임에 입력하지 않는다.
4. **모든 계정 모델 불러오기**로 로그인한 각 계정의 모델 합집합을 가져온다. provider·계정·모델명으로 검색한다. 40행씩 페이지를 이동하며 전체 결과에 접근한다. 이름이 같은 모델도 provider와 계정 표시로 구분한다.
5. 목록에서 모델을 검토하고 **이 모델로 선택**을 누른다. 서버가 확정한 선택만 하단의 현재 플레이 모델로 표시한다.
6. **내 구독 사용 허용**, 필요시 **초기 월드 생성 1회 허용**을 명시적으로 누른다. 이 버튼들은 설정만 저장하며 비상상황 생성이나 NPC 추론을 자동으로 시작하지 않는다.

초기 인과조건과 NPC 계획은 같은 선택을 사용한다. 플레이 중에는 계정·모델·동의 변경을 잠그며, 훈련을 종료한 뒤 변경하도록 안내한다. 연결 실패가 다른 provider/계정/모델이나 API 키 과금으로 자동 전환되지는 않는다.

## UI / host / native client 경계

`ProviderSelectionPanel.Create(Transform parent, TMP_FontAsset font, INativeProviderMenuHost host)`가 진입점이다. 패널은 상태 DTO를 읽고 host의 액션만 호출한다. 네트워크, helper capability, OAuth token, 파일 저장, 외부 URL 실행을 직접 처리하지 않는다.

`INativeProviderMenuHost`와 `NativeProviderControlClient`는 C# client/host lane 소유다. host는 비동기 요청 결과를 Unity 메인 스레드에서 반영하고, provider의 OAuth URL만 브라우저로 연다. 상태 조회, 로그인·poll·취소·로그아웃, 계정별 catalog, 선택, 구독동의와 초기 1회 허용을 native API로 전달한다. 선택의 내부 8필드와 구현/포트/SDK 정보는 제품 기본 화면에 표시하지 않는다.

격리 런타임의 assembly definition에도 실제 어셈블리 이름 `UnityEngine.UI`, `Unity.TextMeshPro`를 참조해야 한다. 설치된 DLL을 전역으로 참조한 API 컴파일 성공과 staging recipe의 assembly 참조 정합은 별도로 확인한다. recipe 검토를 위해 실제 staging이나 Unity 실행을 할 필요는 없다.

메뉴는 화면을 덮는 raycast 영역과 기존 스타일의 3개 열로 구성한다. 왼쪽은 provider·계정, 가운데는 모델 검색·목록·페이지, 오른쪽은 검토·로그인 안내다. 하단은 실제 확정된 모델·구독 사용 상태·필요한 액션을 유지한다. 명칭과 외부 모델 문자열은 TMP rich text로 해석하지 않는다.

## 입력과 폰트

- 패널은 마우스·uGUI 선택, `Tab`/`Shift+Tab` 포커스 이동, `Esc` 닫기를 처리한다. 닫기 전에 항상 host에 로그인 취소 의도를 전달한다. 아직 flow 응답을 받지 않은 auth/start 요청도 host에서 취소할 수 있어야 한다.
- host는 메뉴가 열릴 때 FPS 캡처와 장비·대응 입력을 차단하고 커서를 해제한다. 닫을 때는 다음 프레임에 여전히 플레이 중이고 앱에 포커스가 있는 경우에만 이전 캡처를 복원한다. 같은 Esc 입력 프레임에서 FPS 컨트롤러와 실행 순서가 충돌하지 않도록 한다.
- 기존 EventSystem을 재사용한다. 없을 때만 패널 소유의 `InputSystemUIInputModule`과 기본 액션을 만들고 파괴 시 정리한다.
- 한국어 `TMP_FontAsset`는 host가 제공해야 한다. 원본 참고 자산은 `Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset`이며 원본 Noto 폰트와 dynamic atlas를 사용한다. 현재 격리 프로젝트에 해당 자산이 staging/연결됐다는 증거는 없다. font가 없는 상태를 영어 전용 기본 폰트로 성공 처리하지 않는다.

## 검증과 배포 상태

이번 작업은 소스 작성과 설치된 Unity 어셈블리를 대상으로 한 **엔진 없는 API 컴파일**, native DTO/host 계약 검토 범위다. 현재 UI 소스의 hash와 일치하는 40개 소스·182개 참조 API 컴파일이 통과했다(`output/simulator/checks/unity-source-compile.json`). 합성 계정으로 실행한 C# loopback HTTP 계약 검증도 통과했다(`output/simulator/native-provider-controls/receipt.json`): 두 provider의 같은 모델명 구분, 로그인·모델목록·선택·동의·로그아웃, 409 이후 상태 회복, 플레이 중 변경 잠금, 취소 뒤 늦은 응답 거부를 확인했다. 실제 provider 추론은 0회였다. UI 검증을 위해 Unity Editor, PlayMode, 빌드나 그래픽 캡처를 실행하지 않았다. API 컴파일과 실제 클릭·한국어 glyph·포커스·레이아웃·FPS 입력 복원 검증은 구분한다.

helper의 새 native-control API는 **2026-10-14 04:57 UTC 배포 receipt 기준 적용 완료**다(`workers/simulator/provider_auth/native-control-deployment-receipt.json`). 현재 소스 로드, native 상태 HTTP 200, 스키마 일치와 기존 계정·브라우저 세션 파일의 바이트 보존을 확인했다. 이 검증에서 모델 선택 없음, 구독 사용 꺼짐, 초기 생성 허용 0회를 유지했고 실제 로그인·catalog 새 조회·모델 추론은 모두 0회였다. helper 배포 확인은 원본 Unity 프로젝트에 새 화면을 설치하거나 실제 화면을 검증했다는 의미가 아니다. 프로세스 번호는 제품 문서에 고정하지 않는다.

남은 실제 적용은 사용자 보류가 해제된 이후의 작업이다: 한국어 폰트 staging/연결, 원본 소유 writer가 승인된 소스 통합, Unity 실제 메뉴·브라우저 인증·모델 선택·입력 복원 확인. 구독 모델의 NPC 계획 → NavMesh 이동과 시각 수용성 역시 아직 검증되지 않았다. 추가 실제 provider 추론이나 사용자 quota 소모는 이 UI lane에서 하지 않았다.
