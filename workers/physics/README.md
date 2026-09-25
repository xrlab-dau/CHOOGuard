# 국소 기준 공간 물리 워커

실행: `python3 workers/physics/runtime_package.py` — 아래에서 해당 플랫폼 package를 먼저 조립한다. 개발 venv/PATH의 Python으로 자동 우회하지 않는다.

이 워커는 부산역 또는 부산 시가지의 검증된 디지털 트윈이 아니다. 30×20×4 m 단층 기준 공간과 2 m 병목을 계산한다. 게임 시가지에 표시할 때도 이 계산 범위를 명시해야 한다. 시나리오 문서 생성·내보내기는 없다.

## 실제 계산

- JuPedSim 1.4.2 `CollisionFreeSpeedModelV3`, dt=0.05 s. 기존 실행 근거는 macOS ARM64이며 새 Windows package 실행 증거와 구별한다. 자동 경로와 충돌 회피로 출구 영역에 도달해 엔진이 제거한 인원만 대피 인원이다.
- NIST FDS 6.11.1 공식 x86_64 `fds_openmp`, Rosetta, 2 OpenMP threads. 실제 성공한 0–120 s batch reference output을 사용한다. 0.5 m mesh, 19,200 cells. 계산 덱과 로그·원시 slice·수치 CSV·해시는 `cases/reference-hall/`에 있다.
- FDS x/y → 워커 x/z, FDS z는 높이. 1.5 m 단면의 extinction [1/m], 온도 [°C], soot density [kg/m³], CO/CO2/O2 체적분율을 샘플링한다. 시각 범위 밖에서는 오류를 반환하고 마지막 필드를 재사용하지 않는다.
- [pyFDS-Evac](https://github.com/PedestrianDynamics/pyFDS-Evac) 원본 세 모듈의 `SliceFieldSampler`, Lund smoke speed, toxic/convective-heat FED 함수를 재사용한다. 핀은 `upstream-lock.json`. 전체 프로젝트 GUI/runner를 설치하거나 실행한 것은 아니다. package의 `physics/upstream`에 들어간 해시 검증 원본을 별도 namespace로 로드한다. FDS reader의 pickle cache는 비활성화하여 배포 입력에 쓰지 않는다.
- 연기는 실제 지역별 FDS extinction을 통해 보행 희망속도를 낮춘다. 독성/대류열 누적 FED는 각각 적분한다. 서로 더하지 않으며 실제 사망·임상 결과로 해석하지 않는다. 복사열 인체영향과 HCN/자극가스는 미지원이다. 이 워커는 열/FED 임계치에 의한 무력화도 구현하지 않았다.
- FDS batch → JuPedSim의 단방향 연결이다. 경보·대피 명령은 보행 상태에만 작용한다. 문·환기·소화 변경을 FDS에 다시 계산해 보내지 않는다.

## 상태·단위

`HELLO {protocolVersion:1}` 이후 `SUBMIT {runId,generation,seed,population,scenario,action,stepSeconds}`. `scenario`: `fire_smoke` 또는 `crowd_medical`. `action`: `start`, `advance`, `warn`, `evacuate`, `medical`. `stepSeconds`: 0–1 s, 0.05 s 배수. Unity에서는 1 simulated second마다 요청한다. 최대 인원 200. 새 generation은 새 start가 필요하다. CANCEL은 해당 generation을 fence한다. 한 워커는 한 active session만 소유한다.

`start`는 경보 전 정지 상태이다. warn은 인식 상태만 기록하며 release하지 않는다. evacuate의 all/staged/hold 정책이 집단별 이동 허용을 바꾼다. medical은 지원 요청 상태를 기록하며 임상 효과를 생성하지 않는다. `start_routine`은 기존 50명 평시 기준 운영이며 일반 gameplay의 수백 NPC 자율성 구현을 대체하지 않는다.

`density`: 반경 2 m 이내 인원 / 원 면적의 최대값 [person/m²]. 경계 절단 면적 보정은 하지 않는다. `pressureIndicator`: 같은 이웃의 밀도 × 1초 구간 보행속도 분산 [1/s²]. 압착 접촉력·압력 Pa가 아니다. `visibility`: 활동 인원의 최소 가시거리 `3/K`, 화면 표시는 30 m 상한. 온도·soot·extinction은 활동 인원 위치의 최대값이다. 의료 요청, FED, units/frame/fieldTime/inputDigest가 추가 필드로 전달된다. crowd-only의 온도20°C와 가시거리30m는 명시적 무화재 기준값이며 FDS 계산값이 아니다.

JSONL UTF-8 stdout만 프로토콜로 사용한다. stderr는 진단. 1 MiB line read cap, 64 KiB inline cap, depth32, duplicate key/NaN/unknown kind 거부. 최대 200명 선언은 모든 부가 위험장/결과가 64 KiB에 들어간다는 보장이 아니다. 이웃 지표는 여전히 전수검색이다. 동기 1초 이하 작업 경계에서 CANCEL을 처리하고 stdin EOF에서 종료한다. Unity는 EOF 후 제한 시간 내 종료하지 않는 워커를 종료·회수한다. 별도 heartbeat scheduler나 과학 엔진의 durable checkpoint/replay는 아직 없다. 후속 계산에 실패하면 physicsReady=false ERROR이고 Unity는 기존 값으로 성공 처리하면 안 된다.

## 실행 근거와 재실행

`evidence/reproduction-summary.json`은 실제 두 세션 기록이다. 인구72 무화재 대피와 인구24 화재(25 s 지연 후 대피)를 실행했다. 원시 request/result JSONL도 보존한다. 이것은 실행·연결 증거이며 격자수렴, 모델 검증, 실제 역사 보정 또는 전문 안전평가가 아니다.

## 플랫폼 package와 의존성

Python 3.11 이상을 **패키징 도구**로만 사용한다. 실행용 Python은 package 안의 CPython 3.13.15이며 가상환경 복사가 아니다.

```sh
# Windows 실행 없이도 official ZIP/wheel 다운로드·검증·조립 가능
python3 workers/physics/package_runtime.py --platform win-x64
python3 workers/package_broker.py --platform win-x64
# arm64 macOS에서 명시적인 SQLite native 컴파일을 포함
python3 workers/physics/package_runtime.py --platform osx-arm64 --compile-sqlite
python3 workers/package_broker.py --platform osx-arm64
```

기본 출력은 `workers/runtime/runtime-manifest.json`과 플랫폼별 디렉터리다. 기존 플랫폼은 덮어쓰지 않는다. 교체 package는 새 `--output`에서 생성하고 검증 후 배포한다. 런처는 기본 출력 또는 명시적인 `CG_RUNTIME_PACKAGE` root만 읽고, 경로 이탈·symlink·파일 누락·SHA-256 불일치를 거부한다. Python은 `-I -B -u -X utf8`, argv/비셸 실행으로 공백·한글 경로를 보존한다. 패키징 다운로드 캐시는 `workers/physics/.package-cache`에만 쓴다.

`runtime-artifacts.lock.json`에 download URL, 실제 upstream checksum 출처, license, 버전을 기록한다:

- SQLite 3.53.4 Windows x64 DLL: [공식 download metadata](https://sqlite.org/download.html)의 SHA3-256과 내려받은 ZIP을 대조한 뒤 DLL의 SHA-256을 manifest에 기록. [Public domain](https://sqlite.org/copyright.html). macOS는 같은 검증 amalgamation을 `clang -dynamiclib -arch arm64`로 빌드하며 compiler/flags를 별도 provenance에 남긴다.
- Windows CPython: [Python 3.13.15](https://www.python.org/downloads/release/python-31315/)의 embeddable ZIP과 공식 SPDX SHA-256. macOS는 [Astral standalone 20260924](https://github.com/astral-sh/python-build-standalone/releases/tag/20260924)의 release asset SHA-256. 원본 라이선스 고지를 보존한다.
- Wheel은 PyPI의 플랫폼별 CPython 3.13 artifact와 SHA-256을 고정한다. JuPedSim `deprecated~=1.2.18` → **1.2.18**, 그 전이 의존 `wrapt>=1.10,<2` → **1.17.3**. NumPy 2.5.3/Shapely 2.1.2/fdsreader 1.11.9/typing-extensions 4.16.0은 유지한다.
- JuPedSim GUI용 PySide6/VTK는 명시적으로 제외하고 metadata는 고치지 않는다. 따라서 전체 JuPedSim 배포에 대한 `pip check` 성공으로 표현하지 않는다. headless imports와 Windows 실행은 별도 검증 항목이다.
- JuPedSim의 미포함 `MSVCP140.dll`은 pinned NumPy wheel의 동일 원본 DLL을 원래 이름으로 package에 넣으며 binary hash와 source member를 `msvc-provenance.json`에 남긴다. Windows-only Microsoft Distributable Code 조건은 Python `LICENSE.txt`에 보존한다. JuPedSim LGPL source archive도 배포 package에 포함한다. 실제 재배포에서는 해당 라이선스/최종 사용자 약관을 준수해야 한다.
- runtime FDS는 기존 완료 reference output을 해시 검증하여 재생한다. Windows live FDS solver를 설치/실행했다고 주장하지 않는다.
- gameplay broker는 [공식 Node 24.21.0](https://nodejs.org/dist/v24.21.0/SHASUMS256.txt), 다섯 `.mjs` 모듈, SHA-512 integrity가 고정된 AJV 의존성, 원본 두 JSON schema를 같은 platform manifest에 추가한다. `workers/node-runtime.lock.json`이 Node 출처다. npm install scripts/키/계정 설정은 package에 넣지 않는다. `BrokerStartInfo(capability)`는 capability와 schema 경로를 환경변수로 전달하며 provider 설정은 명시적인 실행 환경에서만 받는다. Windows child는 kill-on-close Job으로 소유한다. 기존 `/turnaround` 서비스와는 별개다.

2026-09-25 Windows package 조립은 실제 수행했다(physics/SQLite 1,290 files, broker 포함 1,839 files). **Windows Player/native DB/worker 실행, 일반 사용자 권한, crash/restart/disk-full: NOT_RUN**. DLL 다운로드·checksum 검증은 실행 수용이 아니다.

## Unity 빌드·저장 연결

`BuildBaseline.Build`에는 기존 `-cgBuildTarget/-cgBuildRoot/-cgBuildOutput` 외에 실제 모델 scene asset인 **`-cgGameplayScene Assets/...unity`**가 필수다. Bootstrap-only를 제품 빌드로 받지 않는다. 명시적 build에서만 `ChooGuardGameplay` entry scene을 Unity API로 임시 생성해 `GameplayBootstrap.WorldSceneName`으로 모델을 additive load한다. 기존 씬 YAML을 수정하지 않으며 생성 씬만 제거하고 활성 씬을 복원한다. 대상 플랫폼 package 전 파일을 검증·복사하여 Player `StreamingAssets/ChooGuardRuntime`에 넣고 빌드 receipt inventory에 포함한다.

`ChooGuard.Persistence.RuntimePackage.ConfigureRoot(absoluteRoot)` 후 `GameplayJournal(absoluteDatabasePath)`를 사용한다. Player 저장 파일은 일반 사용자 writable 경로에 두고 부모 디렉터리를 먼저 생성한다. 기존 `SqliteProvider`의 exact-file hash/source-ID 검증, WAL/FULL, busy timeout, 기존 agency schema를 재사용한다. Windows는 `LoadLibraryExW`로 package DLL과 System32만 허용하며 메모리 DB/시스템 SQLite fallback은 없다.

- `Append(runId,generation,sequence,kind,actorId,json)`, `Read(runId)`, `SaveCheckpoint(runId,generation,sequence,json)`, `ReadCheckpoint(runId)`.
- `AppendAndCheckpoint(runId,generation,sequence,kind,actorId,json,checkpointJson)`는 둘을 한 `BEGIN IMMEDIATE` transaction에 저장한다.
- 동일 run/gen/seq의 다른 payload/owner는 충돌이며 exact retry만 idempotent다. 새 generation은 sequence를 다시 시작할 수 있다. 최신 checkpoint보다 오래된 새 append와 checkpoint 회귀는 거부한다.
- entry JSON 1 MiB, checkpoint 16 MiB, run당 entry 100,000개, 전체 payload/키 예산 256 MiB. 초과 시 명시적 오류이며 기억/증거를 삭제하거나 조용히 성공 처리하지 않는다. SQLite FULL의 implicit rollback도 원래 오류를 가리지 않는다.
- gameplay 의미 저장은 과학 엔진 내부 checkpoint나 fresh LLM 호출의 동일 재현 보장이 아니다.

## Unity Build Automation — Windows x64

로컬 Editor/import에서 자동 실행하지 않는다. 아래 설정을 **명시한 UBA Windows builder**에서만 실행한다. Cloud 실행/과금 허용 여부는 별도 승인 범위이며 이 hook은 빌드를 예약하거나 provider를 호출하지 않는다. 여기서 원격 build/test 성공을 주장하지 않는다.

### Dashboard 설정 계약

기존 GitHub 저장소의 **별도 게시 branch**를 연결하고 Unity project subdirectory는 저장소 root로 둔다. Git LFS 파일이 있다면 실제 내용을 checkout해야 한다.

| UBA 설정 | 값 |
| --- | --- |
| Unity version | `6000.3.23f1` |
| Build platform / architecture | Windows standalone / x86_64 (`StandaloneWindows64`) |
| Builder operating system | **Windows**. macOS cross-build 아님 |
| Development build | 켬. Mono backend는 Pre-Export가 명시하며 실제 BuildPlayerOptions도 검사 |
| Advanced Settings → Script hooks → Pre-Build Script | `workers/cloud-prebuild.sh` |
| Pre-Build Script failure blocks build | 켬 (`preBuildScriptFailsBuild=true`) |
| Pre-Export Method | `ChooGuard.Editor.Bootstrap.CloudBuildHooks.PreExport` |
| Post-Export Method | `ChooGuard.Editor.Bootstrap.CloudBuildHooks.PostExport` |
| Post-Build Script | 비움 |
| Advanced Settings → Scenes → Scene List | 아래 두 항목을 **순서대로**, `Assets/` 기준 상대 경로로 지정 |
| Addressables / Asset bundles | 이 target에서는 끔. Post-Export는 가장 최근 **Player** BuildReport를 검증 |
| Caching | 끔 (`remoteCacheStrategy="none"`). 잔존 runtime 없는 clean checkout 사용 |
| Auto-build / schedule | 꺼둠. 서비스 활성화/과금 조건을 승인한 뒤에만 수동 build 시작 |
| Machine / timeout | `win_micro_v1` (Windows Micro, 8 CPU / 16 GB), 프로젝트 timeout 45분. Boost disk 사용 안 함 |

Scene List:

1. `ChooGuard/GeneratedCloudBuild/ChooGuardGameplay.unity`
2. `ChooGuard/Scenes/FpsStation.unity`

첫 scene은 Pre-Export가 `BuildBaseline.CreateGameplayEntry`로 생성한다. `GameplayBootstrap`의 실제 FPS 진입점, seed/atomic-transition JSON, Korean font를 포함하고 두 번째 실제 모델 scene을 additive load한다. 기존 `FpsStation.unity`를 저장하거나 수정하지 않는다. 단순 Bootstrap/lab 빌드로 대체하지 않는다. 실제 build 옵션의 scene 목록/순서, Development, Windows x64, Mono가 다르면 실패한다. UBA가 export를 담당하므로 `-executeMethod BuildBaseline.Build`를 추가하여 두 번 빌드하지 않는다.

REST API에서는 `platform="standalonewindows64"`, `settings.unityVersion="6000_3_23f1"`, `settings.operatingSystemSelected="windows"`를 쓴다. Script hooks, `playerExporter`, test flags는 모두 **`settings.advanced.unity`** 아래에 있다. `playerExporter.export=true`, `playerExporter.buildOptions=["Development"]`, `playerExporter.sceneList`는 위 순서다. `editorUserBuildSettings.standaloneBuildSubtarget="Player"`, `assetBundles.buildBundles=false`, `addressables.buildAddressables=false`, `preBuildScriptFailsBuild=true`로 둔다. 네 test flag는 `runUnitTests`, `runEditModeTests`, `runPlayModeTests`, `failedUnitTestFailsBuild`이며 모두 `true`다. target 환경변수는 별도 `PUT /orgs/{orgid}/projects/{projectid}/buildtargets/{buildtargetid}/envvars`의 `{"envvars":{...}}`로 설정한다. 비활성 준비 target은 `enabled=false`, `settings.autoBuild=false`로 보존한다. `settings.architecture`는 Player가 아니라 **Editor** architecture이므로 대상 x64 지정 대신 쓰지 않는다.

Advanced Settings → Environment variables:

| 변수 | 값/조건 |
| --- | --- |
| `CG_CLOUD_BUILD` | `win-x64` |
| `CG_GAMEPLAY_SCENE` | `Assets/ChooGuard/Scenes/FpsStation.unity` |
| `CG_CLOUD_PYTHON` | 선택: native Windows x64 Python **3.11 이상** `python.exe`의 절대 경로. 미설정 시 `python` 사용. Cygwin Python 불가 |

필요한 Python이 없는 image에서는 명시적으로 실패하며 SDK/Editor를 자동 설치하지 않는다. UBA 문서는 Python이 있다고 명시하지만 3.11 이상인지는 선택 image에서 확인해야 한다. 별도 .NET SDK/NuGet CLI 설치는 필요 없다. 기존 경계 회귀 테스트에는 **실제 Windows symbolic-link 생성 권한**(Developer Mode 또는 `SeCreateSymbolicLinkPrivilege`)이 필요하다. 권한이 없으면 prebuild가 실패한다. 가짜 파일/테스트 제외로 통과시키지 않는다.

UBA 내장 `IS_BUILDER=true`, `BUILDER_OS=WINDOWS`, `PROJECT_DIRECTORY`, `OUTPUT_DIRECTORY`, `DEVOPS_ENV`를 사용한다. Bash는 Cygwin에서 실행하므로 native Python에 넘기는 경로를 `cygpath -wa`로 변환한다. helper는 `DEVOPS_ENV`에 `CG_CLOUD_OUTPUT_DIRECTORY`, `CG_RUNTIME_PACKAGE`, `CG_TEST_SQLITE_BINARY`, `CG_TEST_SQLITE_SHA256`, `CG_TEST_SQLITE_SOURCE_ID`, `UNITY_EXTRA_PARAMS`를 쓴다. 마지막 변수에는 기존 값을 보존하면서 실제 외부 scratch의 `-cgFixtureRoot` 및 `-cgBuildLinkFixture`가 추가된다. Dashboard에서 이 두 인수를 중복 지정하지 않는다. 원격 test command에도 전달됐는지는 첫 UBA 실행 로그로 확인해야 하며 누락되면 테스트가 실패해야 한다.

### Clean checkout 검증과 runtime 조립

Pre-Build Script는 Unity의 최초 script compilation **이전**에 다음을 수행한다.

1. 외부 scratch에 실제 root/parent/leaf/dangling symlink fixture를 생성한다.
2. 이미 Git에 추적된 DotRecast 2026.3.1 세 DLL을 `workers/nuget-managed.lock.json`의 netstandard2.1 경로/SHA-256 및 `Assets/NuGet.config`/`Assets/packages.config` 선언과 대조한다. `BootstrapValidator`도 같은 선언/버전/경로/해시를 확인한다. 추가 DLL, 다른 framework DLL, 누락/변조/선언 drift는 실패한다. `Assets/Packages` 전체를 허용하지 않으며 managed package 복원이나 CLI 다운로드를 수행하지 않는다.
3. 기존 `package_runtime.py --platform win-x64`, `package_broker.py --platform win-x64`의 동일 구현을 호출하여 `workers/runtime/`를 새로 조립한다. 기존 lockfile SHA-256/SHA3-256/npm SHA-512 검증과 라이선스 보존은 그대로다. 기존 package를 덮어쓰지 않는다.

Pre-Export는 기존 Bootstrap 검증과 전체 runtime hash 검증을 재사용한다. Post-Export는 해당 Player의 실제 성공 BuildReport를 요구하며 기존 `CopyRuntimePackage`로 `*_Data/StreamingAssets/ChooGuardRuntime`를 채운다. 복사 후 해시를 확인하고 전체 runtime을 receipt inventory에 포함한다. `chooguard-build-receipt.json`을 Player 옆에 추가하며 원본 receipt는 기존 `docs/build/evidence/CS-BOOT.01.01/<runId>/` 형식이다. 원격 출력 경로는 checkout과 겹치면 안 된다. Unity가 Post-Export 전 실패한 경우 UBA 실패 로그/test report가 근거이며 hook receipt의 존재를 보장하지 않는다. **receipt의 Player `runStatus`는 `NOT_RUN`으로 유지한다.**

게시 source closure에는 gameplay C#/asmdef/meta 및 참조 scene/asset 외에도 다음이 필요하다:

- `Assets/NuGet.config`, `Assets/packages.config`, 기존 추적 중인 `Assets/Packages/`의 세 DotRecast package와 meta, `Packages/manifest.json`, `Packages/packages-lock.json`, `workers/nuget-managed.lock.json`.
- 두 runtime lockfile, `workers/package_broker.py`, `workers/physics/package_runtime.py`, `worker.py`, `upstream-lock.json`, `requirements-core.txt`.
- `workers/physics/cases/reference-hall/`의 receipt와 해시가 가리키는 원시 FDS 출력/덱.
- `workers/prediction/`의 다섯 gameplay `.mjs` 모듈 및 `package.json`, `package-lock.json`.
- `docs/CHOOGuard_Story_Plan_v5/design/fps-ai-20260925/contracts/npc-decision.schema.json`, `future-step.schema.json`, `docs/build/baseline.schema.json`.

`workers/runtime/`, `.package-cache/`, venv, node_modules, credentials, raw Unity/Hub 로그는 게시하지 않는다. 기존 추적된 DotRecast DLL은 위 managed lock으로 제한한다. 새로 조립한 native runtime/provenance/license는 저장소 대신 **빌드 artifact**에 포함한다.

### 원격 테스트와 남은 수용 한계

Advanced Settings → Tests에서 **Run my project's unit tests when building**, **Run EditMode tests**, **Run PlayMode tests**, **Mark build as failed if any test fails**를 모두 켠다. test/assembly/category filter로 known failure를 숨기지 않는다. Mac-host-only `WindowsBuild_OnMac...` 검증만 `UnityPlatform(OSXEditor)`로 적용 범위를 명시한다. 기존 navigation portal 회귀는 그대로이며 실패하면 target도 실패해야 한다. SQLite 테스트는 cloud에서 조립한 exact DLL/hash/source-ID를 환경변수로 받는다.

UBA의 Test summary뿐 아니라 NUnit 결과의 passed/failed/skipped와 로그를 확인한다. EditMode/PlayMode 통과도 일반 사용자 Windows Player 실행, UI/실제 모델 충돌/portal 이동, native DB/worker 동작, crash/restart/disk-full 수용을 대신하지 않는다. provider key는 아직 없으며 키 없이 LLM 수용 성공을 주장하거나 build에 키를 넣지 않는다. provider 인증/유료 호출은 별도 승인 및 런타임 설정 사항이다.

공식 근거:

- [UBA custom scripts, Pre-/Post-Export signatures, DEVOPS_ENV, Cygwin paths](https://docs.unity.com/en-us/build-automation/advanced-build-configuration/run-custom-scripts-during-the-build-process)
- [UBA environment variables](https://docs.unity.com/en-us/build-automation/reference/available-environment-variables), [installed software](https://docs.unity.com/en-us/build-automation/reference/installed-software)
- [UBA Scenes override](https://docs.unity.com/en-us/build-automation/advanced-build-configuration/specify-the-scene-to-be-built), [unit test settings](https://docs.unity.com/en-us/build-automation/reference/unit-tests)
- [BuildPlayerProcessor](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.BuildPlayerProcessor.html), [BuildReport.GetLatestReport](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Reporting.BuildReport.GetLatestReport.html)
- [현재 UBA OpenAPI schema](https://build-api.cloud.unity3d.com/api/v1/api.json), [Development build API 설정](https://support.unity.com/hc/en-us/articles/46849099987220-Toggling-the-Development-Build-Setting-via-Unity-Build-Automation-API)

## 오프라인 FDS 재계산

`python3 workers/physics/run_reference.py --solver-manifest /absolute/package/solver.json --case /absolute/new-case`를 사용한다. fresh case에는 검토한 `hall.fds`만 복사한다. manifest 필드: `schemaVersion:1`, 상대 `executable`, `binarySha256`, `solverVersion:"6.11.1"`, `solverRevision`, 원본 배포 `source`, `artifactSha256`. 실행 전에 package 경로/바이너리 해시/고정 덱을 검사한다. 이전 receipt/log가 있는 case는 덮어쓰지 않고, 실제 process exit와 FDS 완료 로그가 있어야 completed를 기록한다. 라이브 Player의 자동 대체 solver가 아니다.

기존 `cases/reference-hall/receipt.json`의 macOS/Rosetta FDS 실행 출처와 이전 `evidence/` 결과는 역사적 증거로 유지한다. 새 platform package의 검증 결과로 재사용하지 않는다.
