# CS-BOOT.01.01 구현 계획

상태: **NEEDS_RECONCILIATION** · revision `3-tmp-stage-observed` · candidate / fixture

승인 계획 `~/.claude/plans/distributed-churning-garden.md`의 구조화 인계다. 새 설계를 추가하지 않는다. 제품은 모두 **planned / NOT_PRODUCED**이며 이 단계에서 Assets/Packages/ProjectSettings/.gitignore를 쓰지 않았다.

## 경계 정합화

- 정본 writes/outputs에 승인 제품 표 및 probe 기본 ProjectSettings 21개를 반영했다. 최초 경계 writes 37개에 실제 필요한 TMP 7개와 개별 metadata 15개를 추가하여 현재 writes는 59개다. 최대 8경로의 과거 시험은 보존했다.
- Editor/EditMode/PlayMode asmdef의 최초 작성자는 01.01, 후행 02.01/03.01은 수정(`MODIFY`)이다. 스키마의 CREATE/MODIFY를 사용하며 도구·스키마를 바꾸지 않았다.
- dependency/acceptance/testKinds/status는 유지한다. Windows Player 수용을 이동·완화하지 않는다.
- 정확한 allowedWrites와 관측 generatedWrites/hash는 PLAN.json, 미생성 .meta의 null hash는 GENERATED_FILES.json을 따른다. probe hash는 root 제품 hash가 아니다.

## 남은 입력 정합화

EXT-UNITY는 실제 macOS Editor 초기화·기존 라이선스 관측에 한해 SATISFIED다. TMP 7개 및 metadata 15개의 scratch 실측 hash/recursive closure와 새 프로세스 NUnit 2/2 PASS를 고정했다. root 경계 검토만 남아 있으며 root 복사·제품 생성·PLAN_READY 승격은 하지 않았다. 상태 장부 records/claims는 비어 있다. `.remember` 외부 로그 drift는 보존·기록하며 blocker가 아니다.

## 검증과 증거

새 소유권 assertion RED → 정본 최소 변경 → 같은 두 회귀 GREEN. 전체 Python suite의 rdflib 미설치 오류를 제품 실패나 전체 GREEN으로 바꾸지 않는다. 실행 명령·정확 수치는 `docs/build/evidence/CS-BOOT.01.01/20260920T002150Z-ecbf11/verification.json`에 기록한다. Root Unity 제품 시험은 아직 NOT_RUN이다. TMP scratch 시험은 실제 settings 부재 assertion RED → 최종 코드의 guard/자원 시험 2/2 PASS다. 최신 계획 회귀는 46개 중 43 PASS, rdflib 부재 3 ERROR이며 별도 available subset은 43/43 PASS다. 상세는 `docs/build/evidence/CS-BOOT.01.01/20260920T002730Z-b4c5b4/verification.json`을 따른다.

## 승인된 구현 계약과 후속 실행 (모두 계획, 실행 주장 아님)

## 구현 계약

### 환경과 패키지

초기 공식 manifest 후보: URP 17.3.0, uGUI 2.0.0, TMP 5.0.0, Test Framework 1.6.0, Input System 1.20.0. 앞 네 버전은 설치 Editor descriptor에서 확인했다. Input System 1.20.0은 Unity 6000.3 공식 페이지의 available version이다: https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.inputsystem.html

이 값들은 최초 계획 당시 resolve 전 후보였으며, 현재 scratch에서 실제 resolve된 lock/hash를 manifest에 고정했다. root lock은 아직 없다. 공식 registry/설치 package만 사용하고 UPM에서 실제 resolve 후 lock을 생성한다. 손으로 transitive lock을 위조하거나 local file/absolute path/git package를 넣지 않는다. 충돌하면 낮은 effort에서 임의 버전 변경 없이 Terra에 관측 결과를 전달한다. Editor/OS 라이선스 probe가 실패하면 candidate 제품 작성은 중단하고 BLOCKED_INPUT; 라이선스 설치/인증/유료 취득을 자동 수행하지 않는다.

EXT-UNITY는 실제 사용할 macOS Editor·사용 가능한 라이선스·지원 OS의 관측으로 판정한다. SATISFIED는 **Windows Player 수용을 뜻하지 않는다**. Windows module/host 없음은 별도 build/run NOT_RUN 사유로 남긴다. `UNKNOWN/MISSING/SATISFIED` 외 상태를 externalInputs에 넣지 않는다.

### TMP 최소 의존성 구체화 — Terra 기술 검토 반영


다음은 기존 generatedWrites(c)의 구체화이며 새 제품 기능이나 공개 API 추가가 아니다. 전체 Essential은 scratch stage에서만 import한다. 최대 허용 8경로 중 실제 shader closure에 필요한 아래 7개 리소스 및 해당 파일·부모 디렉터리의 `.meta`만 root 복사 후보로 선별했다. 실제 생성 hash와 의존성을 관측하여 인계에 고정한 뒤 복사한다.

- `Assets/ChooGuard/Settings/Resources/TMP Settings.asset`
- `Assets/ChooGuard/Settings/TMP/Fonts/ChooGuard Bootstrap SDF.asset`
- `Assets/ChooGuard/Settings/TMP/Shaders/TMP_SDF-Mobile.shader`
- `Assets/ChooGuard/Settings/TMP/Shaders/TMPro_Properties.cginc`
- `Assets/ChooGuard/Settings/TMP/LineBreaking/Leading Characters.txt`
- `Assets/ChooGuard/Settings/TMP/LineBreaking/Following Characters.txt`
- `Assets/ChooGuard/ThirdPartyNotices/LiberationSans-SDF-OFL.txt`

`AssetDatabase.CopyAsset`으로 원 static SDF를 복제하고 font/material/atlas는 동일 asset의 sub-asset으로 유지한다. 사용자에게 보이는 asset/sub-asset/family 이름은 `ChooGuard Bootstrap SDF` 계열로 바꾼다. OFL 원문·원저작자 고지는 수정하지 않는다. 별도 material/atlas 파일, 원본 TTF/OTF/TTC, dynamic fallback, EmojiOne, Default Style Sheet, demo/sample은 root로 복사하지 않는다.

정적화는 `atlasPopulationMode=Static`만으로 끝내지 않는다. 실제 설치 TMP source의 직렬화 필드를 확인하여 fallback table을 비우고 `m_SourceFontFileGUID`, `m_SourceFontFilePath`, `m_SourceFontFile` 및 creation settings의 source/referenced font/text GUID·source filename을 null/empty로 정리한다. `m_SourceFontFile_EditorRef`는 비직렬화 internal Editor 캐시로 별도 제한 reflection으로 정리하고 새 프로세스에서 null을 관측했다. 최소 새 `TMP_Settings`는 default font만 연결하고 global fallback/sprite/style/emoji/color-gradient 참조·경로를 비우되 leading/following line-breaking TextAsset 두 개는 보존한다. Shader는 Essential의 mobile SDF와 실제 include closure만 사용하며 shader `.meta` GUID를 보존한다.

EditMode는 source/fallback 직렬화 참조 제거, font/material/atlas 각각 1개, shader `TextMeshPro/Mobile/Distance Field`, non-null line-breaking 참조와 recursive `AssetDatabase.GetDependencies` whitelist를 확인한다. PlayMode는 marker 각 문자의 glyph 존재 및 visible character/mesh vertex, unexpected font/shader log 부재를 확인한다. scratch 생성 성공을 root 제품 검사나 Player 렌더 성공으로 주장하지 않는다. 외부 Player 배포 시 OFL 고지 동봉을 별도로 확인하며, 이번 기술 판단을 법률상 확정으로 표현하지 않는다.


현재 실행 범위는 scratch stage에 한정한다. 전체 Essential이 필요하면 scratch에서만 import한다. 아직 root 제품 생성/복사, candidate claim 또는 PLAN_READY 승격을 하지 않는다.

### Assembly

- 초기 `ChooGuard.Editor`: includePlatforms=[Editor], Unity editor/UI/TMP/Input System/URP에 필요한 package assembly만 참조한다. 없는 App/World/Presentation을 미리 참조하지 않는다.
- EditMode asmdef: includePlatforms=[Editor], TestAssemblies, ChooGuard.Editor 및 실제 사용하는 Unity package references.
- PlayMode asmdef: includePlatforms=[], TestAssemblies, UI/TMP/InputSystem references; Editor/ChooGuard.Editor 참조 금지. 시험 코드는 UNITY_INCLUDE_TESTS로 한정하여 일반 Player에 포함되지 않게 한다.
- package assembly 이름은 resolve된 실제 asmdef에서 읽어 고정한다. 모든 새 C#은 하나의 custom asmdef 아래에 속하며 predefined Assembly-CSharp로 유입되지 않아야 한다.

### Scene와 재실행

- scene 이름 Bootstrap, root Camera 1개, Screen Space Overlay Canvas 1개, 활성 EventSystem 1개.
- Canvas: CanvasScaler 1280×720 ScaleWithScreenSize, GraphicRaycaster, 화면 중앙 TMP label `CHOOGuard bootstrap`. label raycastTarget=false, 정적 SDF font/material/atlas 참조를 저장한다. 임의 UI 버튼·런타임 controller는 넣지 않는다.
- EventSystem은 `InputSystemUIInputModule` 하나만 사용하며 StandaloneInputModule은 없다. 기본 UI actions를 실제 직렬화해 point/click/submit/cancel이 null 또는 비활성 action으로 남지 않게 한다.
- GraphicsSettings/QualitySettings의 유효 pipeline이 BootstrapURP를 가리키고 Renderer 참조가 유효하다. 활성 quality에 따라 Built-in으로 떨어지지 않아야 한다.
- PlayerSettings는 Mono/Development 기준, EditorBuildSettings에는 Bootstrap만 enable. 실제 Windows target/module 확인 없이 Windows build가 가능하다고 표시하지 않는다.
- 생성기는 자기 소유 경로만 변경하고 재실행 시 scene/root/EventSystem이 중복되지 않는다. 기존 asset GUID를 가능한 한 보존한다. 초기화 도중 실패하면 baseline/build success를 쓰지 않고 실패 로그를 남긴다. 재실행은 동일 소유 파일에서만 수행한다.

### 내부 API (namespace `ChooGuard.Editor.Bootstrap`)

```csharp
public static class BootstrapProject
{
    public static void CreateBootstrapAssets();
    public static void WriteBaseline();
    public static void BuildDevelopmentPlayer();
}

public static class BootstrapValidator
{
    public static BootstrapSnapshot Capture(string projectRoot);
    public static BootstrapValidationReport Validate(BootstrapSnapshot snapshot);
}

public sealed class BootstrapValidationReport
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }
}
```

BootstrapSnapshot은 Editor에서 읽은 값만 담는 테스트 가능한 DTO다: `EditorVersion:string`, `ProjectVersion:string`, `PackageLockSha256:string`, `MissingRequiredPackages:string[]`, `LocalPluginPaths:string[]`, `EventSystemCount:int`, `CameraCount:int`, `CanvasCount:int`, `HasInputSystemModule:bool`, `HasLegacyInputModule:bool`, `InputActionsValid:bool`, `TmpResourcesValid:bool`, `UrpReferencesValid:bool`. Capture는 실제 filesystem/PackageInfo/scene에서 수집하며 prefab/text 문자열 검색을 실제 component 관측으로 대체하지 않는다. Validate는 파일을 변경하지 않는다.

EditMode 시험 namespace/class는 `ChooGuard.Tests.EditMode.Stories.CSBOOT0101Tests`로 고정한다. Capture는 현재 사용자 scene 설정을 보존하고 소유 Bootstrap scene을 검사한 뒤 복원한다. 시험용 additive scene의 EventSystem은 runtime의 중복 가능성을 검증할 때만 함께 센다. 누락된 Bootstrap은 0개/참조 false로 관측하고 파일 권한/파싱 오류는 예외를 숨기지 않는다.

판정 오류 코드는 정렬·중복 제거된 문자열: EDITOR_VERSION_MISMATCH, PACKAGE_LOCK_MISSING, REQUIRED_PACKAGE_MISSING, LOCAL_PLUGIN_UNDECLARED, EVENT_SYSTEM_COUNT, CAMERA_CANVAS_MISSING, INPUT_MODULE_INVALID, INPUT_ACTIONS_MISSING, TMP_RESOURCE_MISSING, URP_REFERENCE_MISSING. LocalPlugins는 Assets 아래 .dll/.so/.dylib/.bundle로 제한하고 Unity registry package 내부의 정상 의존성을 로컬 DLL로 오탐하지 않는다. 이번 플러그인 allowlist는 비어 있다.

`IsValid`는 **로컬 정적 검증 통과**만 의미하며 Windows 실행 수용이 아니다. CLI 실패 시 전체 errors를 기록하고 nonzero exit; interactive menu에서는 예외/오류를 보이되 Editor를 강제 종료하지 않는다.

BuildBaseline은 제품 운영 wire API가 아니라 이번 빌드 receipt DTO이며 다음 형식을 schema로 고정한다. 모든 object는 additionalProperties=false, SHA256은 소문자 64hex, enum 오타/중복 JSON key/NaN을 거부한다.

| 필드 | 형식 및 의미 |
|---|---|
| editorVersion | 실제 Application.unityVersion 문자열, ProjectVersion과 일치 |
| packageLockSha256 | 실제 lock 원시 바이트 hash |
| renderPipeline | `{name:"URP", packageVersion:string, assetPath:string, assetSha256:string}` |
| backend | `{target:"StandaloneWindows64", architecture:"x86_64", scriptingBackend:"Mono", development:true}` — 목표 설정, 실행 주장 아님 |
| os | `{name:string, version:string, architecture:string}` — 실제 probe host |
| nativePlugins | `{path:string, sha256:string}[]`, 이번 정상 결과는 [] |
| buildReceiptRef | `{path:string, sha256:string}` — repo-relative 실제 새 receipt 파일 |

build receipt는 `schemaVersion:1`, storyId, runId, sourceTreeDigest, editorVersion, packageLockSha256, host, requestedTarget, buildStatus, runStatus, reason, outputs를 갖는다. status는 NOT_RUN/SUCCEEDED/FAILED. outputs는 실제 파일 `{path,sha256}` 목록. Windows 부재이면 buildStatus=NOT_RUN/runStatus=NOT_RUN이며 이유를 기록한다. macOS smoke build를 수행하면 별도 receipt로 남기고 Windows receipt를 바꾸지 않는다. sourceTreeDigest는 receipt/evidence 자체를 제외한 제품 Assets/Packages/ProjectSettings 및 schema 파일의 경로+hash에 대해 계산하여 자기참조를 피한다.

## 실행 단계

### Task 1 — 환경 probe, 경계 정합화, 컴파일 가능한 시험 기반

**Files:** 위 정본/파생 경계 파일, 3개 asmdef, manifest/lock/settings, handoff.

- [ ] 현재 git 상태와 입력 hash를 재측정한다. 무관한 사용자 변경은 보존하고 관련 계약/경로 충돌만 차단한다. PLAN에 원시 파일 hash와 canonical plan/story digest를 구별해 넣는다.
- [ ] scratchpad `csboot0101-probe`에 새 빈 프로젝트를 Editor CLI로 생성하고 초기화 로그/exit code/OS/라이선스 사용 가능 여부를 기록한다. `-batchmode -nographics -quit -createProject`는 초기 환경 probe에만 사용한다. 성공 exit 하나가 아니라 정상 초기화 로그와 ProjectVersion 생성까지 확인한다.
- [ ] 위 package 조합을 resolve하고 필요한 실제 generatedWrites를 추출한다. font asset은 static SDF이며 원본 TTF/OTF 배포 참조가 없는지 검사한다. font가 없으면 fake label 통과가 아니라 TMP_RESOURCE_MISSING.
- [ ] 소유권 회귀 시험을 먼저 추가하고 기존 정본에서 의도한 assertion RED 확인 후 최소 정본 변경 및 파생 동기화를 한다.
- [ ] EXT-UNITY 관측과 claim을 기록한다. PLAN.json에 generatedWrites/실제 package lock/PLAN revision을 고정한다. 적합한 입력 확인 뒤 `PLAN_READY`; source drift 또는 범위 밖 파일이면 NEEDS_REPLAN.
- [ ] 검증된 설정과 최소 asmdef를 root로 복사한다. 실제 NUnit 시험 1개 수집·실행으로 하네스 동작부터 확인한다. 이 하네스 PASS를 제품 PASS로 보고하지 않는다.

계획 검증 명령(제품 시험 아님):

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -B docs/CHOOGuard_Story_Plan_v4/tools/plan.py validate
PYTHONDONTWRITEBYTECODE=1 python3 -B -m unittest discover -s docs/CHOOGuard_Story_Plan_v4/tests -p 'test_*.py'
PYTHONDONTWRITEBYTECODE=1 python3 -B docs/CHOOGuard_Story_Plan_v4/tools/render_plan.py --check
```

의존성이 없으면 패키지를 자동 설치하지 말고 해당 문서 검사만 NOT_RUN으로 기록한다. 기존 검증 실패를 제품 패치 탓으로 단정하지 않는다.

### Task 2 — Scene 생성·진단 및 정상/반례 TDD

**Files:** BootstrapProject.cs, BootstrapValidator.cs, BuildBaseline.cs, EditMode/PlayMode testFile, scene/URP/TMP assets, baseline schema.

- [ ] 컴파일 가능한 최소 API skeleton과 아래 시험을 작성한다. 생성기 미구현 상태에서 scene 부재 또는 validator 누락 진단에 대한 assertion RED를 기록한다. C# 미정의 타입/패키지 오류를 RED로 대체하지 않는다.
- [ ] Capture/Validate와 생성기를 최소 구현한다. 테스트가 매번 생성기를 자동 실행해 손상된 제출 scene을 고치지 않도록, **제출 scene 검사**와 **격리 생성기 시험**을 분리한다.
- [ ] 필요한 Unity API는 resolve된 package source/공식 문서에서 확인하고 구현한다. asset YAML을 추측해 손으로 만들지 않는다.
- [ ] static baseline schema와 실제 값 작성 기능을 구현한다. Python `planlib.read_json`+jsonschema를 인계 검사에 재사용하며 새 범용 JSON parser를 만들지 않는다.
- [ ] EditMode GREEN 후 PlayMode GREEN을 수행하고 generator 재실행 전후 GUID/중복/원치 않은 파일 변경을 검사한다.

최소 시험 골격(이름과 oracle을 유지하고 정리/복원을 구현한다):

```csharp
[Test]
public void BootstrapScene_HasOneCameraCanvasAndEventSystem()
{
    var snapshot = BootstrapValidator.Capture(ProjectRoot);
    Assert.That(snapshot.CameraCount, Is.EqualTo(1));
    Assert.That(snapshot.CanvasCount, Is.EqualTo(1));
    Assert.That(snapshot.EventSystemCount, Is.EqualTo(1));
}

[Test]
public void DuplicateEventSystem_IsRejected()
{
    var snapshot = BootstrapValidator.Capture(ProjectRoot);
    snapshot.EventSystemCount = 2;
    Assert.That(BootstrapValidator.Validate(snapshot).Errors,
        Does.Contain("EVENT_SYSTEM_COUNT"));
}
```

`ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))`로 정의한다. 위 snapshot 반례에 그치지 않고 아래 실제 컴포넌트·파일 관측 검사도 수행한다. Bootstrap scene이 없는 초기 상태에서 Capture는 0개와 누락 진단을 반환하여 시험이 명시한 assertion에서 실패하게 한다. scene을 열 수 없는 I/O 오류는 제품 RED와 구별하여 기록한다.

| 시험 | 실제 oracle |
|---|---|
| 정상 제출 scene | Camera/Canvas/EventSystem 정확히 1, missing script 없음, 유효 URP/TMP/actions |
| 두 EventSystem | 임시 additive scene 또는 preview scene에 실제 두 component 생성 → Capture가 2 관측 → 지정 오류. finally에서 scene 해제 |
| 로컬 DLL | scratch filesystem fixture의 Assets/Plugins/LocalOnly.dll 발견 → LOCAL_PLUGIN_UNDECLARED. root Assets에 가짜 assembly를 넣어 compiler를 깨지 않음 |
| 누락 패키지 | 필수 package 하나가 없는 snapshot에서 REQUIRED_PACKAGE_MISSING. 실제 Capture가 registered package와 필수 목록을 대조하는 별도 검사. UPM resolve 실패는 환경 실패로 별도 기록 |
| 손상된 font/actions/URP | 해당 참조 제거 fixture에서 TMP_RESOURCE_MISSING / INPUT_ACTIONS_MISSING / URP_REFERENCE_MISSING 각각 발생 |
| 생성기 두 번 | 동일 owned assets에서 두 번 생성 후 EventSystem 여전히 1, 불필요한 GUID 교체/scene 중복 없음 |
| baseline | schema+중복키 검사, actual lock/editor/URP asset hash 일치, receipt가 실제 파일이며 새 run/hash와 일치 |
| 잘못된 receipt/hash | 경로 탈출/누락 파일/hash 불일치 또는 잘못된 status를 수용 거부 |

PlayMode는 Bootstrap을 실제 `SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single)`로 열고, 최소 2 frame 후 다음을 검사한다. namespace는 `ChooGuard.Tests.PlayMode.Stories`, class는 `CSBOOT0101PlayModeTests`로 고정한다.

```csharp
[UnityTest]
public IEnumerator BootstrapScene_RendersStaticMarkerAndUsesInputSystem()
{
    yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
    yield return null;
    yield return null;
    var systems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
    Assert.That(systems.Length, Is.EqualTo(1));
    Assert.That(systems[0].currentInputModule,
        Is.TypeOf<InputSystemUIInputModule>());
    var marker = Object.FindFirstObjectByType<TextMeshProUGUI>();
    Assert.That(marker, Is.Not.Null);
    Assert.That(marker.text, Is.EqualTo("CHOOGuard bootstrap"));
    marker.ForceMeshUpdate();
    Assert.That(marker.textInfo.characterCount, Is.GreaterThan(0));
}
```

추가 oracle: font/material/atlas가 null이 아니며 visible glyph/vertex가 존재, Canvas enable, actions 연결 및 enable, runtime 예외/누락 shader가 없다. Headless 검사만으로 실제 화면 렌더 확인을 주장하지 않는다. 입력 모듈 활성/연결 검사는 실제 포인터 장치·OS IME 수용을 뜻하지 않는다.

### Task 3 — 실제 실행·부팅 증거·독립 검수 및 종료

**Files:** docs/build baseline/receipt/evidence, IMPLEMENTATION/REVIEW handoff. 코드 결함 수정은 여전히 Astra low만 담당한다.

- [ ] 매 호출마다 존재하지 않는 새 output dir를 만든다. Unity CLI의 정확 Test Framework 1.6 옵션은 resolved package 문서/실행 출력에서 확인한다. 아래 직접 CLI를 사용하며 아직 없는 scripts/Run-UnityTests.ps1을 호출하지 않는다.
- [ ] `-runTests` 실행에 `-quit`를 붙이지 않는다. 테스트 종료는 runner에 맡기고 제한 시간 종료를 실패/NOT_RUN으로 분류한다. render 검증에 `-nographics`를 붙이지 않는다.

```sh
UNITY='/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/MacOS/Unity'
PROJECT='${REPO_ROOT}'
# OUT은 호출자가 만든 이번 run의 새 절대경로이다. 기존 결과 재사용 금지.
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests \
  -testPlatform EditMode -testFilter ChooGuard.Tests.EditMode.Stories.CSBOOT0101Tests \
  -testResults "$OUT/editmode.xml" -logFile "$OUT/editmode.log"
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests \
  -testPlatform PlayMode -testFilter ChooGuard.Tests.PlayMode.Stories.CSBOOT0101PlayModeTests \
  -testResults "$OUT/playmode.xml" -logFile "$OUT/playmode.log"
```

- [ ] 정상 종료코드와 신선한 NUnit XML을 함께 확인한다. total>0, failed=0, skip/inconclusive=0, 실제 기대 class/test 이름을 확인한다. raw RED와 GREEN 모두 보존한다. timeout 후 다른 사용자의 Unity 프로세스를 종료하지 않는다.
- [ ] 가능한 경우 그래픽이 있는 Editor에서 Game View 표식/URP 오류/Console을 확인한다. 도구로 화면을 관찰할 수 없으면 MANUAL_NOT_RUN으로 남긴다.
- [ ] MacStandaloneSupport로 optional macOS 개발 Player smoke build/run을 한다. 이 결과는 별도 local receipt다. `BuildDevelopmentPlayer`는 `-cgBuildTarget`(StandaloneOSX 또는 StandaloneWindows64), `-cgBuildOutput`(이번 scratch run 아래 경로)을 명시적으로 받고, module 지원 여부 확인 후 BuildReport의 실제 결과를 기록한다. 미지원 target을 현재 target으로 자동 변경하지 않는다. build 출력은 프로젝트/기존 파일을 덮어쓰지 않는다.
- [ ] Windows x64 module/실행 host가 없으므로 Windows build/run receipt를 NOT_RUN으로 남긴다. 자동 toolchain 설치·원격 머신 실행은 하지 않는다. 실제 Windows 실행이 가능해졌다면 동일 고정 입력으로 새 Player 부팅, 표식·EventSystem·로그·version receipt를 별도로 입증해야 한다. 이번 Mac 결과로 AC-CS-BOOT.01.01-P 전체나 AT-066을 PASS 처리하지 않는다.
- [ ] raw log/테스트 결과와 입력 hash를 고정하고 sourceTreeDigest를 기록한다. finalRevision은 새 commit이 없으므로 만들어내지 않고 HEAD+treeDigest를 사용한다. 미추적 새 파일은 `git diff`에 안 나오므로 changedPaths/파일 hash 목록을 별도로 비교한다.
- [ ] Terra가 실행·패키지·테스트 증거, Sol이 범위·계약·미실행 표시를 검토한다. 실제 관련 시점에만 호출하며 동일 탐색/설계를 반복하지 않는다. 필요 ECC C# review checklist를 이 검토에 적용한다. 독립 검수는 별도 실행이지 사람/기관 독립 승인이라는 의미가 아니다.
- [ ] 변경 후 Graphify의 설치 CLI를 확인해 코드만 AST 갱신한다. 기존 semantic nodes를 보존하는 동작이 확인되면 `.agents/rules/graphify.md`의 `graphify update .`를 실행한다. 전체 문서 재추출·외부 API·기존 semantic graph 손실을 요구하면 실행하지 않고 graph STALE/UNKNOWN 및 changed paths를 handoff에 남긴다. freshness를 거짓으로 올리지 않는다.
- [ ] claim을 RELEASED로 바꾸고 IMPLEMENTATION/REVIEW를 완성한다. 정본 상태와 runtime receipts를 혼합하지 않는다. 후속 스토리는 추천만 가능하며 실행하지 않는다.


## 중단·인계

TMP scratch generatedWrites와 최종 NUnit 2/2 PASS 관측을 완료했으며 root 경계 검토 전 NEEDS_RECONCILIATION. 정본 digest/raw hash는 PLAN.json/CONTEXT.json을 따른다. 실제 제품 작성 때만 claim, 종료 때 RELEASED; records에 ACCEPTED/SUBMITTED를 쓰지 않는다. repo-root 제품과 docs-root 장부 경계는 우회하지 않는다. 범위 밖 변경·동일 원인 두 번 실패는 계획자로 반환한다. commit/stage/push/PR/외부 업로드 및 Graphify 수정은 이 작업에서 수행하지 않는다.

## Scratch 실측 인계 경계

- compact 후보: `docs/build/evidence/CS-BOOT.01.01/20260920T002730Z-b4c5b4/root-copy-candidate.json` — settings 21, packages 2, TMP 7, 코드/asmdef 5, metadata 26의 총 61파일. stage hash와 root null hash를 구분한다.
- font/material/atlas 3 subasset, Static, persistent source/fallback 제거 및 transient Editor cache null을 source tree 제거·reimport 후 새 프로세스에서 확인했다. Assets closure는 TMP 7개이며 registry script 2개와 Editor icon 1개를 별도 기록했다. OFL 원문과 shader/include GUID·bytes를 보존했다.
- `GraphicsSettings.asset`는 제외된 기본 URP global asset 참조를 가진 관측 초기 설정이다. `REQUIRES_OWNED_URP_REWRITE`로 유지한다. 후속 승인 제품 단계가 Unity API로 소유 `BootstrapURPGlobalSettings.asset`을 생성·등록하고 GraphicsSettings/QualitySettings를 바꾼 뒤 최종 검증한다. 제외 자산을 추가 복사하지 않는다. 초기 설정을 완결 제품/GREEN/baseline으로 주장하지 않으며 새 설계 결정이나 TMP stage 차단 사유로 취급하지 않는다.
- stage-only helper와 guard 시험은 root에서 그대로 완성 API가 아니다. 후속 구현에서 기존 승인 BootstrapProject/Validator/BuildBaseline 계약으로 확장한다. PlayMode·scene·URP·실제 graphics·Player는 NOT_RUN이다.

## Revision 4 — Root implementation gate

`PLAN_READY`: Root reported inspection of all 61 actual candidate hashes, containment and missing root paths. Evidence: `docs/build/evidence/CS-BOOT.01.01/20260920T010925Z-20b06c/boundary-inspection.json`. The earlier pending-root statements describe revision 3, now superseded. Current canonical ownership includes 70 paths; root implementation and actual scene-missing RED are authorized. No ACCEPTED/SUBMITTED record.
