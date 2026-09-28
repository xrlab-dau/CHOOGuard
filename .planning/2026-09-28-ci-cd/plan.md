# CI/CD 구축 — 2026-09-28

브랜치 `ci/production-pipeline` (develop `b8098810` 기준). 요구: OSS·OpenSSF·벤치마크·JEV 판정 기반의 프로덕션 수준 CI/CD 구축과 적용. #245(검증기 실패)·#246(CI 부재) 해소.

## 판정 기록 (JEV authority 0, 최종 결정은 메인·결정론적 검증)

| 항목 | 판정 | 근거 파일 |
|---|---|---|
| Unity 활성화 스위치 | 비밀값 3개 + 저장소 변수 `UNITY_CI_ENABLED=true` (0.84) | jev-cicd-014, jev-cicd-016 |
| Unity 시험 호스트 | GitHub macOS arm64 러너 네이티브 설치 (0.99) | jev-cicd-016 |
| Unity 빌드 | 같은 macOS 러너, 프로젝트 자체 `PlayerBuild.BuildMac` (0.93) | jev-cicd-016 |
| 시트 동시성 | 저장소 전체 단일 그룹, 실행 중 취소 금지 (0.76) → `queue: max` | jev-cicd-016 |
| PR 범위 | 같은 저장소 브랜치 PR 중 Assets/Packages/ProjectSettings 변경만 (0.89) | jev-cicd-016 |
| Unity 필수 체크 | 첫 녹색 라이선스 실행 후 'Unity tests' 필수화 (0.83) | jev-cicd-016 |
| 시험/빌드 분리 | PR은 시험, develop push·야간·태그는 시험+빌드 (0.80) | jev-cicd-014 |
| Library 캐시 | 시험 잡 하나만 캐시, 빌드는 새로 임포트 (0.82) | jev-cicd-014 |
| 빌드 대상 | macOS만 (0.50) | jev-cicd-014 |
| 필수 체크 | 정책 게이트 + 도구 시험 (0.56) | jev-cicd-014 |
| 릴리스 | main 태그 → 초안 릴리스 + 빌드 출처 증명 (0.97) | jev-cicd-014 |
| 보안 | 전체 벤치마크 묶음 (0.98) | jev-cicd-014 |
| graphify | 신선도 경고만 (0.91) | jev-cicd-014 |
| PR 제목 규칙 | 없음 (0.91) | jev-cicd-014 |
| PR 라벨 | 복원 (bool 0.61) | jev-cicd-014 |
| progress.json 잔여 키 | 제거 (0.60) | jev-progress-key-015 |

## 전제 변경 (2026-09-28)

- 사용자: Unity 계정이 학생 플랜으로 변경. Unity 학생 플랜 안내는 메일로 받은 라이선스 키를 Hub에 추가하라고 한다 → 시리얼 방식. GameCI 문서상 Professional 경로(`UNITY_SERIAL`·`UNITY_EMAIL`·`UNITY_PASSWORD`).
- Unity FAQ: Pro/Enterprise 시트 하나는 동시에 두 대까지 활성화(학생 플랜 동일 가정, 미검증). 이 Mac이 한 자리 사용.
- 호스트 가정: `SqliteProvider`는 macOS 전용(다른 OS는 `PlatformNotSupportedException`), `CSOPS0201/0203` 사용, `CSBOOT0101Tests:447`은 `OSXEditor` 단정 → GameCI 리눅스 컨테이너에서는 구조적으로 실패.
- GameCI `unity-test-runner` v4.3.2는 리눅스/윈도 도커 전용, 컨테이너 종료 시 반납 → 취소·시간초과 시 반납 누락.
- GitHub 2026 변경: `queue: max`(동시성 대기열 100), `cache-mode`, 공개 저장소 `pull_request_target` 기본 차단(2026-11-02 강제) → 라벨러는 `pull_request`로.

## 확인된 사실

- 문서 도구 시험 6묶음 로컬 통과(218건). 워커는 단위 시험 없음; 물리 탐침 `run_probes.py`는 실패해도 종료코드 0 → CI가 `passed`를 단정.
- Assets 15,596파일 중 `.meta` 누락 0, 고아 1(`VideoFirstFloor/PropMaterials.meta`, 빈 폴더; `AgentScripts/VideoFloorOne.cs`의 `Folder()`가 재생성).
- 바이너리 `.asset`은 NavMesh·LightingData 3개뿐(Unity 기본). 씬·프리팹·머티리얼은 전부 YAML. `m_SerializationMode: 2`.
- 추적 중인데 무시 규칙에 걸리는 파일 1,800여 개(연구 자료·`Assets/.planning` 과거분) → 위생 규칙은 PR 변경분에만 적용(래칫).
- 추적 중인 `.pyc` 8개, `.gitignore`에 바이트코드 규칙 없음.
- graphify 보고서의 기준 커밋 `6408d1c8`은 스쿼시 병합으로 develop에서 도달 불가 → 신선도는 `graphify-out/graph.json`을 마지막으로 바꾼 커밋 기준.
- `PlayerBuild.Build`는 실패해도 종료코드 0 → CI는 `CG_PLAYER_BUILD result=Succeeded` 표식과 `.app` 존재를 단정.
- 보안 설정: 비밀 스캔·푸시 보호·비공개 취약점 신고·Dependabot 보안 업데이트 모두 꺼짐, CodeQL 기본 설정 미구성.
- Unity 6000.3.23f1 (09d2ecc7fb28) arm64 에디터 pkg 5.1 GB, 설치 9.4 GB.

## 적용 (2026-09-28)

- PR #250 CI 1차: Docs tool suites 실패 — v5 검증기가 참조 파일(Assets 소스·`.planning` 증거)의 존재를 확인하고 진입점 시험이 `prompts/`를 읽는데, 희소 체크아웃에 없었다 → docs 잡은 전체 체크아웃. Dependency review 실패 — 저장소 의존성 그래프가 꺼져 있었다.
- 의존성 그래프: 저장소 단위 REST 토글이 없다. 조직 "GitHub recommended" 구성은 CodeQL 기본 설정을 켜서 고급 워크플로와 충돌한다 → 이 저장소에만 연결한 조직 코드 보안 구성 "CHOOGuard public repository"(id 279373)를 만들었다(`admin:org` 권한은 사용자가 제공한 토큰으로 두 호출만, 저장하지 않음). 비밀 스캔·푸시 보호·Dependabot 보안 업데이트·비공개 취약점 신고도 켰다. CodeQL 기본 설정은 `not-configured` 유지.
- PR #250 CI 2차: 전 잡 통과(Unity 레인은 비밀값 대기로 사유 기록 후 건너뜀).

## PR #249 (Adrianaline, develop 동기화 영수증)

- 검증: JSON 유효, 참조 파일 develop에 존재, 사설 경로 없음. `CommandPreviewPresenter.cs:268`의 `targetsText.isActiveAndEnabled` 가드는 #247에서 추가됨(d61997d0에 없음). develop의 NotoSansCJKkr 폰트는 글리프 311개, 동적 채움 + clear-on-build(#248과 일치). #245 실패 기록은 b8098810 기준 사실이고 #250이 고친다.
- 결함: `updatedUtc` 05:35:00Z가 유일한 커밋(05:10:27Z)보다 늦다.
- JEV `jev-pr249-017`: FORMAT.md는 작성자의 별도 PR로 가져오기 승인(0.95), 타임스탬프는 관리자 수정 커밋(0.79), #250 머지 → #249 브랜치 갱신 → 필수 체크 → 승인 → 스쿼시 머지(0.95).

## 기준선 (2026-09-28)

- develop `3c05e778` push: Quality gate·Tool tests·Security·Unity(gate)·Scorecard 성공. Scorecard 게시 수용, 점수 5.6 (Token-Permissions·Dangerous-Workflow·Dependency-Update-Tool·Vulnerabilities 10, Pinned-Dependencies·Security-Policy 9, Branch-Protection 8, SAST 7, CI-Tests 3; Maintained·Code-Review·License·Binary-Artifacts·Contributors·Fuzzing·CII 0).
- Pinned-Dependencies 감점 2건은 `$/` self-repository 참조를 Scorecard v2.4.4가 인식하지 못한 오탐.
- 필수 체크: `Policy, security and repository hygiene` + `Tool tests` (규칙 22267761). `Unity tests`는 학생 플랜 비밀값 등록·첫 녹색 실행 후.

## 이슈 #248 판정 (NotoSansCJKkr 동적 폰트)

- 원인(TMP 원본, com.unity.ugui 2.0.0): Dynamic/DynamicOS + `clearDynamicDataOnBuild`인 폰트는 `EditorApplication.quitting`(대화형·배치모드 종료 모두)과 `IPreprocessBuildWithReport`에서 `ClearCharacterAndGlyphTablesInternal()`로 문자·글리프 표와 아틀라스를 비우고 저장한다. 커밋된 글리프 수가 커밋 시점에 따라 달라졌다(d61997d0 395개, b8098810 311개).
- 이슈의 "빈 폰트면 한글이 깨진다"는 사실이 아니다: 에디터에서 사본을 0개로 비운 뒤 `TryAddCharacters` 24자 성공(누락 0), 새 한글 TMP 배치 15/15 표시(원본 .otf, includeFontData=1). 플레이어 빌드는 어차피 빈 상태로 시작한다.
- 채운 상태를 커밋하면 아틀라스가 hex로 .asset에 들어가 버전마다 2.27–4.37 MB(압축 0.34 MB)가 쌓이고 한 줄짜리 hex에서 병합 충돌이 난다.
- 최소 Unity 프로젝트에서 실제 종료 3회: 311개판·395개판(아틀라스 2장) 모두 같은 6,528바이트(`3ea6f262…`)가 되고 두 번째 종료에도 그대로다. 저장소에 이 파일을 커밋하면 누구의 종료·빌드·배치 시험에도 차이가 생기지 않는다.
- JEV: 1차(`jev-issue248-018`) flag off 0.50 / 빈 상태+가드 0.44 → 저장소 비용 사실 추가 후 2차(`jev-issue248-019`) 빈 상태+가드 0.72, flag off 0.20. `com.unity.textmeshpro` 5.0.0(의존 없는 폐기 예정 껍데기)은 별도 변경(0.89).
- 조치: TMP가 만든 휴지 상태 파일 커밋, 정책 게이트 `tmp-dynamic-font`(변경분) 추가.

## 모든 운영체제 지원 (사용자 요구 2026-09-28)

- 요구: 게임은 모든 운영체제에서 실행되고 모든 운영체제에서 개발할 수 있어야 한다(팀원 Windows). 학생 플랜이면 Unity Cloud로 옮겨야 하는지 문의.
- 비밀값: `UNITY_EMAIL`(Unity ID dbstkd5865@gmail.com, 2단계 인증 꺼짐)과 `UNITY_SERIAL`(My Seats의 Unity Student 구독 키, 2027-09-28 만료; Aside 브라우저에서 해시로 소속을 확인하고 출력 없이 gh로 전달)을 등록했다. `UNITY_PASSWORD`는 사용자 입력 대기.
- 조사: Unity Build Automation 무료 월 Windows 200분·Mac 100분, 동시 2대(2026-03 공식 공지). GitHub 공개 저장소 러너는 세 OS 무료·무제한. 에디터 설치본 Windows 4.13 GB·Linux 4.46 GB·macOS 5.1 GB. macOS 시스템 SQLite 3.51.0 < 기준 3.51.3.
- JEV `jev-crossplatform-020`: GitHub Actions 다중 OS(0.54, 하이브리드 0.35, UBA 0.11), PR은 Windows·macOS·develop/야간/태그는 Linux 포함(0.41, 전 OS 매 PR 0.31), mac 한 대에서 세 플레이어 빌드 후 OS별 스모크(0.87), SQLite 다중 OS 로더+공식 원본 고정(0.98), .gitattributes(0.89), arm64 유지(0.98).
- 코드: `SqliteProvider` Windows/Linux 로더, `MvpPhysicsBridge` venv 경로, `CSBOOT0101` Windows 호스트 건너뜀, `CSBOOT0201` `GetName()` 대신 `FullName`(한글 경로), `PlayerBuild` Windows·Linux·`BuildAll`. 정적 배칭은 Standalone 그룹 공통이라 변경 없음.
- 로컬 검증(macOS 에디터): 컴파일 오류 0. 고정 SQLite 3.53.4로 CSOPS0201 3건·CSOPS0203 10건 첫 실행 통과, CSBOOT0201 통과, CSBOOT0101 Windows 빌드 시험은 기존대로 -cgFixtureRoot 없이 건너뜀.
- `.gitattributes`: 재정규화 결과 기존 파일 변경 0(CRLF 14건과 연구 자료 1,450건은 바이트 보존 트리).

## 첫 세 OS 실행 (run 36393274297) — self-repository 참조 철회

- `UNITY_PASSWORD`: 계정이 구글 로그인이라 Unity 비밀번호가 없었다. 사용자 승인 후 Security → Change Password 재설정 메일(Aside Gmail)로 페이지 안에서 생성한 무작위 30자 비밀번호를 설정하고, 성공 확인 뒤에만 gh로 전달했다(출력·파일 없음). 재설정 링크는 사용 후 오류 페이지로 바뀌었다. 모든 에디터 로그아웃은 사용자가 승인했다.
- 결과: macOS 잡이 `Set up job`에서 실패했다. `uses: $/.github/actions/setup-unity`는 러너가 codeload에서 저장소 전체 tar.gz를 받게 하는데, 에셋 약 4 GB라 100초 제한을 세 번 넘겼다. Linux도 같은 단계에서 멈췄다. 활성화 전이라 실행을 취소했다(반납할 활성화 없음).
- 조치: 테스트 잡은 루트에 `.github`만 체크아웃하고 프로젝트는 별도 디렉터리(`project`, Windows는 `경로 검사/CHOOGuard`)에 받는다. 로컬 action은 `./.github/actions/...`로 쓴다. zizmor `self-repository` 검사는 이유를 적어 끄고, actionlint `$/` 무시 규칙은 삭제했다.

## 첫 Linux 시험 (run 36394165119) - 시험 입력 누락

- 설치 -> 시리얼 활성화 -> EditMode -> PlayMode -> 반납까지 모두 실행됨. PlayMode 45/45 통과. EditMode 489건: 347 통과, 102 실패, 40 건너뜀.
- 실패 102건은 두 원인뿐이다. 희소 체크아웃에 `content/`가 없어 55건(`content/fixtures/two-agency.json`), `docs/CHOOGuard_Story_Plan_v4/basis/v3/contracts/`가 없어 47건(`assembly-layout.json`)이 실패했다. local-action 변경 전부터 있던 누락이고, 레인이 처음 실제로 돌면서 드러났다.
- 수정: 시험 잡 체크아웃과 게이트 `unity_paths`에 `content`, `docs/build`(영수증·기준 스키마), 계약 디렉터리를 같은 목록으로 추가했다. 로컬 cone 희소 체크아웃으로 세 경로가 있는지 확인했다(디스크 219개 파일).
- 같은 실행의 Windows 잡(비ASCII·공백 경로 첫 실행)은 취소하지 않고 끝까지 돌려 Windows 고유 문제를 먼저 본다.

## 수정 실행 (run 36396147103, `075e9931`) - 시험 입력 수정 확인, Windows 스크립트 결함 2건

- macOS·Linux: EditMode·PlayMode 전부 통과(잡 성공). 시험 입력 누락 수정이 확인됐다.
- Windows: EditMode 449 통과·0 실패·40 건너뜀, PlayMode 45/45 통과. 그런데 잡은 실패했다. 원인은 시험이 아니라 스크립트 두 곳이다.
  1. 판정: `unity_results.py`가 `MODE:PATH:EXIT`를 모든 콜론에서 나눠, Windows의 `D:\a\_temp` 드라이브 문자에서 경로가 잘렸다. 그래서 "no results file (editor exit unknown)"이 나왔다. 첫 실행(36394165119)의 Windows 판정도 같은 오류였는데, 실제 실패 102건에 가려 있었다. 첫 콜론과 마지막 콜론에서 나누게 고치고(`parse_spec`), 실제 CI 문자열로 회귀 시험을 넣었다. 이 실행의 Windows 결과 파일을 새 판정에 넣으면 통과한다.
  2. 반납: "Unity licence returned" 뒤 `rm -rf "$private"`가 "Device or resource busy"로 실패했다(Unity 보조 프로세스가 return.log를 잡고 있음). 첫 실행에서는 성공했으니 경쟁 상태다. 10초까지 다시 지워 보고, 그래도 안 되면 알림만 남긴다. bash 3.2에서 정상·잠김 두 경로를 흉내 내 확인했다.
- 모든 OS의 에셋 임포트가 실제로 일어났다(10,964개, 4-5분). LFS가 없어 러너가 실제 바이너리를 받는다. 문서의 "수십 분" 추정을 측정값으로 바꿨다.
- 다음: `os=windows build=true`로 Windows 수정 확인과 첫 플레이어 빌드·세 OS 스모크를 한 번에 본다.
