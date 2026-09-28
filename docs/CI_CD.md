# CI/CD 운영 문서

2026-09-28 구축. 설계 판단과 근거는 `.planning/2026-09-28-ci-cd/plan.md`(JEV 판정 `jev-cicd-014`, `jev-cicd-016`)에 있다.

## 한눈에

| 워크플로 | 언제 | 잡 | 머지 조건 |
|---|---|---|---|
| `quality-gate.yml` | develop·main 대상 PR, develop·main push | **Policy, security and repository hygiene** | 필수 |
| `tools.yml` | 같음 | Docs tool suites, Worker checks → **Tool tests** | 필수 |
| `unity.yml` | Unity 경로를 바꾼 같은 저장소 PR(Windows·macOS), develop·main push·`v*` 태그·매일 03:00 KST(Windows·macOS·Linux), 수동 | Unity lane gate → EditMode + PlayMode (OS별) → Players (macOS, Windows, Linux) → Player smoke (OS별) → Draft release, **Unity tests** | 첫 녹색 실행 후 필수 |
| `security.yml` | PR, push, 매주 월 04:00 KST | CodeQL(Actions·C#·JS·Python), Dependency review | 정보 |
| `scorecard.yml` | develop push, 매주, 규칙 변경 | Scorecard analysis → scorecard.dev 게시 | 정보 |
| `pr-labels.yml` | 같은 저장소 PR | 경로 라벨(`.github/labeler.yml`) | — |
| Dependabot | 매주 월 09:00 KST, 7일 쿨다운 | Actions 핀, `.github/requirements/*.txt` | — |

필수 체크는 `Protected branches · main + develop` 규칙에 걸려 있다. 필수 잡은 경로 필터 없이 모든 변경에 결과를 낸다. 건너뛴 워크플로는 결과가 없어 PR을 막기 때문이다.

## 정책 게이트 (`.github/scripts/ci_policy.py`)

blob 없이 전체 이력만 받은 체크아웃에서 돈다. 파일 내용은 변경된 Unity 텍스트 에셋만 가져온다.

| 규칙 | 범위 | 판정 |
|---|---|---|
| `unity-meta` | 전체 트리 | `Assets/`(와 임베디드 패키지)의 파일·폴더마다 `.meta`, `.meta`마다 대상. 빈 폴더의 `.meta`는 받은 쪽 Unity가 지우므로 고아로 본다 |
| `generated-files` | 전체 트리 | `Library/`·`Temp/`·`Obj/`·`Logs/`·`UserSettings/`·`Builds/`, 파이썬 바이트코드, `.DS_Store` 금지 |
| `unity-text` | 설정 + 변경분 | `m_SerializationMode: 2`(Force Text). 바뀐 씬·프리팹·머티리얼·애니메이션은 `%YAML`로 시작. NavMesh·LightingData `.asset`은 원래 바이너리라 제외 |
| `tmp-dynamic-font` | 변경분 | Dynamic·DynamicOS이면서 *Clear Dynamic Data On Build*가 켜진 TMP 폰트(`*SDF*.asset`)는 **빈 상태로 커밋**한다. TMP가 Unity를 끌 때와 빌드할 때마다 이 폰트를 비우고, 한글은 쓰는 순간 원본 폰트에서 다시 채운다(#248). Unity를 켠 채 커밋하면 채워진 상태가 섞이므로 그 파일은 커밋에서 빼거나 `git checkout -- <파일>`로 되돌린다 |
| `ignored-files` | 변경분 | `.gitignore`에 걸리는 경로를 강제로 추가하지 않는다(macOS·Windows와 같게 대소문자 무시) |
| `large-files` | 변경분 | 50 MiB 초과 경고, 100 MiB 초과 오류 |
| `branch-flow` | PR | `main`에는 `develop`, `release/*`, `hotfix/*`만 |
| `graph-freshness` | 전체 | `graphify-out/graph.json` 갱신 뒤 색인 대상 파일이 바뀌면 경고만 낸다. `graphify update .` 후 커밋 |

변경분 규칙은 PR이면 머지 커밋과 그 첫 부모, push면 `before..after`를 비교한다. 과거 커밋에 이미 들어간 위반(예: 무시 규칙이 생기기 전 올라간 연구 자료)은 그 파일을 다시 건드릴 때만 걸린다. 같은 잡에서 스크립트 시험, actionlint(+shellcheck), zizmor, 변경분 gitleaks도 돈다.

## 도구 시험 (`tools.yml`)

- `docs/*/tests` 전 묶음을 pytest로 돌리고, v5 계획 검증기 `validate`와 `render --check`를 실행한다(#245 회귀).
- 워커: `compileall`, `node --check`, 물리 검증 탐침 `run_probes.py`. 탐침은 실패해도 0으로 끝나므로 `physics_probes.py`가 판정한다. 모든 사례 통과, 커밋된 `receipt.json`이 현재 스크립트·솔버 소스와 일치, 사례별 결과와 완료 시각이 커밋된 증거와 한 적분 단계(0.05 s) 안이어야 한다. 탐침 스크립트나 `sources/`를 고쳤다면 탐침을 다시 돌려 `receipt.json`을 함께 커밋한다.
- 의존성은 해시 잠금 파일(`.github/requirements/*.txt`)로만 설치한다.

## Unity 레인 (`unity.yml`)

### 세 운영체제 레인

게임은 Windows·macOS·Linux 데스크톱 모두에서 돌아야 하고, 팀원은 Windows, 관리자는 Apple silicon Mac에서 개발한다. 그래서 GitHub 호스팅 러너 세 종류(`windows-2025`, `macos-26`, `ubuntu-24.04`)에 `ProjectVersion.txt`의 에디터를 그대로 설치해 같은 시험을 돌린다. 설치 파일은 OS마다 검증한 뒤에만 설치한다. macOS는 Apple Developer ID 체인과 Unity 팀(`Unity Technologies SF (9QW8UQUTAA)`) 서명, Windows는 Unity Technologies의 유효한 Authenticode 서명, Linux는 Unity 릴리스 매니페스트의 MD5다(Linux 압축본에는 코드 서명이 없다). 에디터는 러너에서 가장 여유 있는 디스크에 설치하고 전후 여유 공간을 기록한다.

- **SQLite 시험**: `SqliteProvider`는 OS마다 정확한 파일만 연다(Windows `LoadLibraryExW`, macOS·Linux `dlopen`). WAL 기준(3.51.3 이상)을 넘는 시스템 SQLite가 어느 OS에도 없으므로, `.github/actions/pinned-sqlite`가 sqlite.org 공식 원본(SHA3-256 확인)으로 3.53.4를 만든다. macOS·Linux는 amalgamation을 컴파일하고 Windows는 공식 DLL을 쓴다. 만든 파일은 `CG_TEST_SQLITE_*` 환경 변수로 넘긴다. 이 변수가 없으면 CSOPS 영속성 시험은 NOT_VERIFIED로 건너뛴다.
- **Windows는 비ASCII 경로에서 시험한다** (`경로 검사/CHOOGuard`). 한글 사용자 폴더에서 Mono 경로 변환이 터진 적이 있다(#249). 공백과 한글이 든 경로를 매번 확인한다.
- **Linux PlayMode**는 가상 디스플레이(`xvfb-run`)에서 OpenGL로 돈다.

### Unity Cloud(Build Automation)를 쓰지 않는 이유

학생 플랜의 실익은 Pro급 에디터 라이선스(시리얼)이고, 이 저장소는 그 시리얼로 GitHub Actions에서 돈다. Unity Build Automation은 2026-03부터 무료가 월 Windows 200분·Mac 100분이다. 동시 실행은 2대까지 무료이고, 그 뒤로는 사용한 만큼 과금한다. 포럼에는 60분 넘는 대기 보고도 있다. 3.9 GB 프로젝트를 PR마다 세 OS에서 시험하면 무료분이 며칠 만에 끝난다. 공개 저장소의 GitHub 호스팅 러너는 세 OS 모두 무료·무제한이고 결과가 PR 체크로 바로 붙는다. JEV 판정도 GitHub Actions(0.54)를 택했다. Build Automation을 릴리스 빌드에만 쓰는 안(0.35)은 필요해지면 추가한다. 조직에는 Unity Cloud 프로젝트 "CHOOGuard Gameplay"가 이미 있다.

### 켜기 (학생 플랜)

학생 플랜은 **라이선스 키(시리얼)** 방식이다. 키는 id.unity.com → My Seats의 "Unity Student" 구독(조직 `dbstkd5865`, 2027-09-28 만료)에 있다. CLI 활성화는 Unity 이메일과 **Unity 비밀번호**가 필요하고, 2단계 인증은 꺼져 있어야 한다. 이 계정은 구글 로그인이라 Unity 비밀번호가 따로 없었다. 그래서 2026-09-28에 Unity Dashboard → Account → Security → **Change Password**(재설정 메일)로 무작위 30자 비밀번호를 만들어 `UNITY_PASSWORD`에만 넣었다. 아무도 이 비밀번호를 모르고, 구글 로그인은 그대로 된다. 다시 만들 때도 같은 방법을 쓴다. 재설정하면 모든 기기의 Unity Editor·Hub가 로그아웃되므로 다시 로그인해야 한다.

```sh
gh secret set UNITY_PASSWORD -R xrlab-dau/CHOOGuard
gh variable set UNITY_CI_ENABLED --body true -R xrlab-dau/CHOOGuard
```

값은 프롬프트로 입력되어 화면과 셸 기록에 남지 않는다. 끌 때는 비밀값을 지우지 말고 `UNITY_CI_ENABLED`를 `false`로 바꾼다. 꺼져 있거나 비밀값이 없으면 `Unity tests`는 이유를 요약에 적고 통과로 보고한다. 필수 체크여도 PR을 막지 않는다.

### 시트 한 개를 나눠 쓰는 규칙

- 시트 하나는 동시에 두 대까지 활성화된다(Unity FAQ, Pro/Enterprise 기준. 학생 플랜도 같다고 가정했고 검증하지 않았다). 관리자 Mac이 한 대, CI가 한 대를 쓴다. My Seats에는 Personal 시리얼로 활성화된 Windows PC `ADMIN`도 보인다. CI 활성화가 한도 초과로 실패하면 쓰지 않는 활성화부터 반납한다.
- 라이선스를 쓰는 잡(세 OS의 시험, 플레이어 빌드)은 저장소 전체 동시성 그룹 `unity-licence` 하나에서 한 번에 하나씩 돈다. `queue: max`라 최대 100개가 순서대로 기다린다. PR의 Windows·macOS 시험도 차례로 돈다. 실행 중인 잡은 취소하지 않는다. 강제로 종료된 에디터는 활성화를 반납하지 못한다.
- 반납은 `if: always()` 단계가 같은 VM에서 한다. 시험 단계가 실패하거나 시간 초과여도 반납은 실행된다.
- "no free activation"으로 실패하면 id.unity.com → **My Account → My Seats**에서 활성화를 반납하고 다시 실행한다.

### 무엇이 언제 도는가

| 이벤트 | 에디터 시험 | 플레이어 빌드 + 스모크 |
|---|---|---|
| 같은 저장소 PR, Unity 경로 변경 | Windows, macOS | — |
| develop·main push(Unity 경로 변경), `v*` 태그, 야간 | Windows, macOS, Linux | ✅ 세 OS |
| 수동 실행 | 선택한 OS 또는 전부 | `build` 입력 |
| 포크 PR, Unity 경로 무변경 | 사유를 남기고 건너뜀 | — |

- Unity 경로: `Assets`·`Packages`·`ProjectSettings`에 더해, EditMode 시험이 저장소 루트에서 읽는 `content/`(픽스처), `docs/CHOOGuard_Story_Plan_v4/basis/v3/contracts/`(어셈블리 계약), `docs/build/`(빌드 영수증·기준 스키마)다. 게이트의 `unity_paths`와 시험 잡의 희소 체크아웃이 같은 목록을 쓴다. 첫 Linux 실행(run 36394165119)에서 체크아웃에 이 셋이 빠져 EditMode 102건이 파일 없음으로 실패했다. 시험이 루트의 다른 파일을 읽게 되면 두 곳에 함께 추가한다.
- 시험: EditMode(`-nographics`)와 PlayMode(Metal, Direct3D/WARP, 가상 디스플레이의 OpenGL)를 한 번의 활성화 안에서 돌린다. 판정은 `unity_results.py`가 결과 XML과 에디터 종료 코드를 함께 보고 내린다. 결과 파일 없음(컴파일 오류·크래시), 0건 실행, 실패, 종료 코드와 결과의 불일치는 실패다. Inconclusive는 경고다.
- 빌드: macOS 러너 한 대가 Windows·Linux Mono 빌드 모듈을 함께 설치하고, 활성화 한 번과 임포트 한 번으로 세 플레이어를 만든다. 진입점은 메뉴와 같은 `ChooGuard.Editor.PlayerBuild.BuildAll`이다. 이 메서드는 실패해도 0으로 끝나므로 대상별 `CG_PLAYER_BUILD target=… result=Succeeded` 표식과 출력물로 판정한다. 산출물은 `player-macos`·`player-windows`·`player-linux`(zip, 30일)다.
- 스모크: 각 OS 러너가 자기 플레이어를 `-batchmode -nographics -soak -soak-shifts 1 -soak-minutes 0.5`로 실행한다. 역사를 불러오고, 새 비상 세션의 군중이 생기고, 플레이한 뒤 타이틀로 돌아와 보고서를 쓰는 전 과정이다. `soak_verdict.py`가 판정한다. 보고서 없음(크래시·멈춤), 근무 누락, 예외, 군중이 생기지 않은 세션은 실패이고, 로그 오류는 경고다. 플레이어는 Unity 라이선스가 필요 없어 세 OS가 동시에 돈다.
- Library 캐시: Windows·macOS 시험 잡만 복원한다. 저장은 develop push에서만 한다(PR은 develop 캐시를 읽기만 한다). Linux와 빌드는 10 GB 캐시 한도를 지키려고 캐시 없이 새로 임포트한다.

### 로그와 비밀값

Unity는 로그 첫머리에 `-serial`·`-password`를 포함한 명령줄 전체를 남긴다. 잡 로그는 GitHub가 가리지만 아티팩트는 가리지 않는다. 그래서 활성화·반납 로그는 업로드하지 않는 별도 폴더에만 쓰고 잡이 끝나면 지운다. 시험·빌드 로그는 비밀값과 시리얼 형태 문자열을 지운 뒤에만 올린다(`unity_ci.sh scrub`).

## 릴리스

1. `develop` → `main` PR을 만들고 머지 커밋으로 병합한다(`main` 규칙).
2. `main`의 머지 커밋에 태그를 붙인다: `git tag v0.2.0 && git push origin v0.2.0`.
3. Unity 워크플로가 세 OS 시험 → 세 플레이어 빌드 → 세 OS 스모크 → **초안 릴리스**(macOS·Windows·Linux zip, `SHA256SUMS.txt`, 빌드 출처 증명)를 만든다. `main`에 없는 커밋의 태그는 거부한다.
4. 초안을 검토하고 게시한다. 받은 쪽 검증: `gh attestation verify CHOOGuard-Windows-x64-v0.2.0.zip -R xrlab-dau/CHOOGuard`.

플레이어는 코드 서명을 하지 않는다. macOS는 처음 열 때 Gatekeeper 경고가 나오고(Apple Developer 계정 필요), Windows는 SmartScreen 경고가 나온다(코드 서명 인증서 필요). macOS 플레이어는 Apple silicon 전용이다.

## Windows·macOS 팀원 개발 환경

- **줄바꿈**: `.gitattributes`가 텍스트를 저장소와 작업 트리 모두 LF로 맞춘다. Windows의 `core.autocrlf` 때문에 Unity가 다시 저장할 때마다 생기던 줄바꿈만의 차이가 사라진다. 연구 자료(`asset-library`), 해시가 기록된 워커 증거, 제3자 라이선스 파일은 바이트 그대로 둔다(`-text`).
- **물리 워커**: `MvpPhysicsBridge`는 Windows에서 `workers/physics/.venv/Scripts/python.exe`, 그 밖에서 `workers/physics/.venv/bin/python`을 찾는다.
- **로컬 SQLite 시험**: 3.51.3 이상 SQLite 파일을 `CG_TEST_SQLITE_BINARY`·`CG_TEST_SQLITE_SHA256`·`CG_TEST_SQLITE_SOURCE_ID`로 지정하면 CSOPS 영속성 시험이 돈다. 없으면 건너뛴다. 만드는 방법은 `.github/actions/pinned-sqlite/action.yml`과 같다.
- **호스트 전제 시험**: `CSBOOT0101`의 Windows 빌드 NOT_RUN 시험은 Windows에서 건너뛴다. Windows 호스트는 실제로 Windows를 빌드하기 때문이다. `CSBOOT0201`은 한글 사용자 폴더에서도 돈다.
- **플레이어 빌드**: 메뉴 `ChooGuard/Build/`에서 macOS·Windows·Linux 플레이어를 만든다. 다른 OS 플레이어를 만들려면 Unity Hub에서 해당 Mono 빌드 모듈을 설치한다.

## 공급망·권한

- 모든 action은 커밋 SHA로 고정한다(태그는 주석). 같은 저장소 action은 `.github`만 받은 체크아웃에서 `./.github/actions/...`로 쓴다. GitHub의 self-repository 문법(`uses: $/...`)은 action을 쓰려고 저장소 전체 압축본(에셋 약 4 GB)을 내려받는다. 첫 실행에서 모든 러너가 이 다운로드의 100초 제한에 걸려 실패했다(run 36393274297). 그래서 zizmor의 `self-repository` 검사는 `.github/zizmor.yml`에서 끈다. 내려받는 도구(actionlint, gitleaks)는 SHA-256을 확인한다. 파이썬 의존성은 해시로 잠근다. Dependabot이 매주 올리되 공개 7일 뒤의 버전만 받는다.
- 워크플로 기본 권한은 `permissions: {}`이고 잡마다 필요한 권한만 준다. 쓰기 권한은 라벨러(`pull-requests`), 보안 결과 업로드(`security-events`), 릴리스(`contents`, `id-token`, `attestations`)뿐이다.
- `pull_request_target`은 쓰지 않는다. 공개 저장소는 2026-11-02부터 기본 차단된다. 체크아웃은 모두 `persist-credentials: false`다.
- 리눅스 잡은 `harden-runner`(audit)로 외부 통신을 기록한다.
- 저장소 보안 설정은 조직의 코드 보안 구성 **"CHOOGuard public repository"**(이 저장소에만 연결)에 있다. 의존성 그래프, Dependabot 알림·보안 업데이트, 비밀 스캔과 푸시 보호, 비공개 취약점 신고(`.github/SECURITY.md`)를 켠다. CodeQL은 고급 워크플로(`security.yml`)로 돌기 때문에 **기본 설정은 꺼 둔다**. 조직의 "GitHub recommended" 구성은 CodeQL 기본 설정을 켜서 고급 워크플로의 결과 업로드를 막으므로 연결하지 않는다.

## 로컬에서 같은 검사 돌리기

```sh
python3 .github/scripts/ci_policy.py --base origin/develop          # 정책 게이트 (커밋된 HEAD 기준)
python3 -m unittest discover -s .github/scripts/tests                 # CI 스크립트 시험
python3 docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py validate
python3 docs/CHOOGuard_Story_Plan_v5/tools/active_plan.py render --check
actionlint -ignore 'unexpected key "queue" for "concurrency" section' \
           -ignore 'specifying action "\$/[^"]+" in invalid format because ref is missing'
uvx zizmor@1.30.1 --offline .
```

잠금 파일 갱신: `.github/requirements/*.txt` 머리말의 `uv pip compile` 명령을 그대로 실행한다.

## 알려진 제약

- actionlint 1.7.12(최신)는 `queue`(2026-05)를 모른다. 이 메시지만 무시한다. 새 actionlint가 나오면 무시 목록을 지운다.
- 표준 러너의 보장 디스크는 14 GB다(에디터 설치 8.1–9.5 GB). 설치 단계가 여유 공간을 기록하고, macOS는 45 GB 미만이면 쓰지 않는 Xcode를 지운다. Windows는 여유가 가장 큰 드라이브에, Linux는 `/mnt` 임시 디스크에 설치한다.
- 시험 잡 하나는 13–25분이다. 대부분이 에디터 설치(Linux 6분, macOS 12분, Windows 19분)다. 첫 임포트(에셋 10,964개, FBX 1,159·텍스처 1,672 포함)는 EditMode 단계 안에서 4–5분 걸린다(Linux 241초, Windows 286초, macOS 308초, run 36394165119). 라이선스가 한 자리라 시험 잡은 한 번에 하나씩 돈다. 세 OS를 모두 돌리면 약 1시간이다.
- Linux 에디터 압축본은 코드 서명이 없어 Unity 매니페스트의 MD5로만 무결성을 확인한다.
- Windows에서는 에디터가 끝난 뒤에도 Unity 보조 프로세스가 `return.log`를 잠시 잡고 있을 수 있다. 반납 단계는 10초까지 다시 지워 보고, 그래도 잠겨 있으면 알림(`::notice`)만 남긴다. 반납은 이미 끝났고, 러너가 잡과 함께 임시 폴더를 지운다.
- 스모크는 헤드리스(`-nographics`)라 시작·씬 로드·세션·종료를 확인하지만 화면 렌더링까지는 보지 않는다. 렌더링은 PlayMode 시험(그래픽 장치 사용)이 맡는다.
- Linux 플레이어는 디스플레이가 없으면 창 백엔드가 null이라 첫 프레임에서 segfault한다(Unity 버그. 실제 사용자는 X11·Wayland가 있어 해당하지 않는다). 그래서 Linux 스모크는 PlayMode 시험처럼 Xvfb 안에서 돈다. 같은 플레이어를 헤드리스와 Xvfb로 나란히 돌려 확인했다(run 36420682952: 헤드리스 exit 139, Xvfb exit 0·오류 0·예외 0).
- `CSBOOT0101`의 경계 재시험(`-cgFixtureRoot` 등 33건, 실제 Bootstrap 빌드 포함)은 아직 CI에서 돌리지 않는다. 세 OS 레인이 녹색이 된 뒤 야간 잡으로 붙인다.
- OpenSSF Scorecard 기준선은 5.6(2026-09-28, `3c05e778`)이다. Pinned-Dependencies 9점의 감점 2건은 당시 쓰던 `uses: $/...` 참조였고, `./` 로컬 action으로 바꾸면서 없어진다. 나머지 감점(저장소 생성 90일 미만, LICENSE 없음, 승인 없는 머지, `Assets/Packages`의 DotRecast DLL 등 바이너리)은 CI 밖의 결정이다.
