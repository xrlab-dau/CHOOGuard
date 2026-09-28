# CI/CD 운영 문서

2026-09-28 구축. 설계 판단과 근거는 `.planning/2026-09-28-ci-cd/plan.md`(JEV 판정 `jev-cicd-014`, `jev-cicd-016`)에 있다.

## 한눈에

| 워크플로 | 언제 | 잡 | 머지 조건 |
|---|---|---|---|
| `quality-gate.yml` | develop·main 대상 PR, develop·main push | **Policy, security and repository hygiene** | 필수 |
| `tools.yml` | 같음 | Docs tool suites, Worker checks → **Tool tests** | 필수 |
| `unity.yml` | Unity 경로를 바꾼 같은 저장소 PR, develop·main push, `v*` 태그, 매일 03:00 KST, 수동 | Unity lane gate → EditMode + PlayMode (macOS) → macOS player → Draft release, **Unity tests** | 첫 녹색 실행 후 필수 |
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

### 왜 macOS 러너인가

`SqliteProvider`는 macOS에서만 SQLite를 열고(다른 OS는 `PlatformNotSupportedException`), `CSBOOT0101Tests`는 `OSXEditor`를 단정한다. 플레이어 빌드 대상도 StandaloneOSX뿐이다. GameCI 시험 러너는 리눅스·윈도 도커 전용이라 구조적으로 실패한다. 그래서 GitHub 호스팅 Apple silicon 러너(`macos-26`)에 `ProjectVersion.txt`의 에디터를 그대로 설치한다. 설치 파일은 Apple Developer ID 체인과 Unity 팀(`Unity Technologies SF (9QW8UQUTAA)`) 서명을 확인한 뒤에만 설치한다.

### 켜기 (학생 플랜)

학생 플랜은 메일로 받은 **라이선스 키(시리얼)** 방식이다.

```sh
gh secret set UNITY_SERIAL   -R xrlab-dau/CHOOGuard   # 학생 플랜 라이선스 키
gh secret set UNITY_EMAIL    -R xrlab-dau/CHOOGuard
gh secret set UNITY_PASSWORD -R xrlab-dau/CHOOGuard
gh variable set UNITY_CI_ENABLED --body true -R xrlab-dau/CHOOGuard
```

값은 프롬프트로 입력되어 화면과 셸 기록에 남지 않는다. 끌 때는 비밀값을 지우지 말고 `UNITY_CI_ENABLED`를 `false`로 바꾼다. 꺼져 있거나 비밀값이 없으면 `Unity tests`는 이유를 요약에 적고 통과로 보고한다. 필수 체크여도 PR을 막지 않는다.

### 시트 한 개를 나눠 쓰는 규칙

- 시트 하나는 동시에 두 대까지 활성화된다(Unity FAQ, Pro/Enterprise 기준. 학생 플랜도 같다고 가정했고 검증하지 않았다). 관리자 Mac이 한 대, CI가 한 대를 쓴다. 다른 PC에서도 활성화돼 있으면 CI 활성화가 실패한다.
- 라이선스를 쓰는 잡(시험·빌드)은 저장소 전체 동시성 그룹 `unity-licence` 하나에서 한 번에 하나씩 돈다. `queue: max`라 최대 100개가 순서대로 기다린다. 실행 중인 잡은 취소하지 않는다. 강제로 종료된 에디터는 활성화를 반납하지 못한다.
- 반납은 `if: always()` 단계가 같은 VM에서 한다. 시험 단계가 실패하거나 시간 초과여도 반납은 실행된다.
- "no free activation"으로 실패하면 id.unity.com → **My Account → My Seats**에서 활성화를 반납하고 다시 실행한다.

### 무엇이 언제 도는가

| 이벤트 | 시험 | 빌드 |
|---|---|---|
| 같은 저장소 PR, `Assets`·`Packages`·`ProjectSettings` 변경 | ✅ | — |
| develop·main push, Unity 경로 변경 | ✅ | ✅ |
| `v*` 태그, 야간, 수동(`build` 입력) | ✅ | ✅ |
| 포크 PR, Unity 경로 무변경 | 사유를 남기고 건너뜀 | — |

- 시험: EditMode(`-nographics`)와 PlayMode(Metal)를 한 번의 활성화 안에서 돌린다. 판정은 `unity_results.py`가 결과 XML과 에디터 종료 코드를 함께 보고 내린다. 결과 파일 없음(컴파일 오류·크래시), 0건 실행, 실패, 종료 코드와 결과의 불일치는 실패다. Inconclusive는 경고다.
- 빌드: 로컬과 같은 진입점 `ChooGuard.Editor.PlayerBuild.BuildMac`을 쓴다. 이 메서드는 실패해도 0으로 끝나므로 `CG_PLAYER_BUILD result=Succeeded` 표식과 `.app` 존재로 판정한다. 산출물은 `player-macos-arm64`(zip + `SHA256SUMS.txt`, 30일)다.
- Library 캐시: 시험 잡만 복원한다. 저장은 develop push에서만 한다(PR은 develop 캐시를 읽기만 한다). 빌드는 캐시 없이 새로 임포트한다.

### 로그와 비밀값

Unity는 로그 첫머리에 `-serial`·`-password`를 포함한 명령줄 전체를 남긴다. 잡 로그는 GitHub가 가리지만 아티팩트는 가리지 않는다. 그래서 활성화·반납 로그는 업로드하지 않는 별도 폴더에만 쓰고 잡이 끝나면 지운다. 시험·빌드 로그는 비밀값과 시리얼 형태 문자열을 지운 뒤에만 올린다(`unity_ci.sh scrub`).

## 릴리스

1. `develop` → `main` PR을 만들고 머지 커밋으로 병합한다(`main` 규칙).
2. `main`의 머지 커밋에 태그를 붙인다: `git tag v0.2.0 && git push origin v0.2.0`.
3. Unity 워크플로가 시험 → 빌드 → **초안 릴리스**(zip, `SHA256SUMS.txt`, 빌드 출처 증명)를 만든다. `main`에 없는 커밋의 태그는 거부한다.
4. 초안을 검토하고 게시한다. 받은 쪽 검증: `gh attestation verify CHOOGuard-macOS-arm64-v0.2.0.zip -R xrlab-dau/CHOOGuard`.

플레이어는 Apple 코드 서명·공증을 하지 않아 macOS에서 처음 열 때 Gatekeeper 경고가 나온다. 없애려면 Apple Developer 계정이 필요하다.

## 공급망·권한

- 모든 action은 커밋 SHA로 고정한다(태그는 주석). 같은 저장소 action은 실행 중인 커밋을 가리키는 `uses: $/...`로 쓴다. 내려받는 도구(actionlint, gitleaks)는 SHA-256을 확인한다. 파이썬 의존성은 해시로 잠근다. Dependabot이 매주 올리되 공개 7일 뒤의 버전만 받는다.
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

- actionlint 1.7.12(최신)는 `queue`(2026-05)와 `$/`(2026-07)를 모른다. 이 두 메시지만 무시한다. 새 actionlint가 나오면 무시 목록을 지운다.
- macOS 표준 러너의 보장 디스크는 14 GB다(에디터 설치 9.4 GB). 실제 여유 공간은 설치 단계가 `df`로 기록한다.
- 첫 Unity 실행과 모든 빌드는 3.9 GB 에셋을 새로 임포트하므로 수십 분 걸린다.
- OpenSSF Scorecard 기준선은 5.6(2026-09-28, `3c05e778`)이다. Pinned-Dependencies 9점의 감점 2건은 `uses: $/.github/actions/setup-unity`다. Scorecard v2.4.4(2026-07-23)가 GitHub의 self-repository 문법(2026-07-30)을 몰라 해시 없는 외부 action으로 오판한 것이다. GitHub는 `$/`를 고정 참조로 취급하므로 점수 때문에 `./`로 되돌리지 않는다. 나머지 감점(저장소 생성 90일 미만, LICENSE 없음, 승인 없는 머지, `Assets/Packages`의 DotRecast DLL 등 바이너리)은 CI 밖의 결정이다.
