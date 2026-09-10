# 학교 PC PM 후속 작업 — 2026-09-08

PM `umyunsang`의 학교 PC 작업이다. 기존 `chore/42-school-pc-setup` 작업과 [설정 기록](2026-09-08-pm-setup.md)을 보존하고 현재 GitHub 상태·설치 도구·검증 근거를 다시 확인했다. 아래는 이 날짜의 조회 결과이며 미래의 보드 상태나 승인 기록을 대신하지 않는다.

## 로컬 시작점

- 저장소: [xrlab-dau/CHOOGuard](https://github.com/xrlab-dau/CHOOGuard). 이미 있는 checkout을 사용한다.
- `git fetch origin develop` 후 HEAD와 `origin/develop`은 모두 `f2011ac2cd4510a09c78f0de111771fc5aa433e8`이다. 작업 브랜치의 미커밋 변경은 별도로 남아 있다.
- [PR #61](https://github.com/xrlab-dau/CHOOGuard/pull/61)은 2026-09-08 06:15:45 UTC에 병합됐다. 병합 당시 feature head는 `4ce3141da4e50335a32b6fa0d87cd1883841959e`다.
- `git lfs fsck` 성공. Unity 실행 파일과 `ProjectVersion.txt` 모두 **6000.3.23f1**이다. Blender **4.5.9 LTS**, GPU는 **RTX 3070 / 8192 MiB**를 확인했다.
- Unity Hub에서 저장소 루트를 열고 `Assets/CHOOguardGenerated/FoundationDemo/FoundationDemo.unity`를 사용한다. Windows 실행본은 로컬 `Builds/FoundationDesktop/ChooGuardFoundation.exe`이며 Data/DLL을 포함한 폴더 전체가 필요하다.

Windows PowerShell에서 저장소 루트를 현재 폴더로 두고 실행한다. `PYTHONIOENCODING`은 현재 터미널의 한글 출력용이며 전역 설정을 바꾸지 않는다.

```powershell
$env:PYTHONIOENCODING = 'utf-8'
git status --short --branch
git lfs fsck
python scripts/context/context_graph.py validate
python scripts/context/context_graph.py brief --topic handoff --machine school-pc
python scripts/dev/check_foundation.py
python -m unittest discover -s scripts/bootstrap -p 'test_*.py'
python -m unittest discover -s scripts/context -p 'test_*.py'
python -m unittest discover -s scripts/ci/tests -p 'test_*.py'
python scripts/ci/repository_policy.py
```

이 명령은 모델·MCP를 설치하지 않는다. 게임 조작은 WASD 이동, 마우스 시점, E 상호작용, Esc 일시정지다. 새 팀원은 기존 checkout의 브랜치를 덮어 바꾸지 않고 별도 checkout에서 `develop` 기반 작업 브랜치를 만든다.

## 보드와 PM 우선순위

[개발 실행 Project 1](https://github.com/orgs/xrlab-dau/projects/1) 82개 항목과 [준비·승인 Project 2](https://github.com/orgs/xrlab-dau/projects/2) 65개 항목을 직접 조회했다. PM에게 배정된 열린 이슈는 35개다. 상태가 같은 것과 설명이 최신인 것은 별개다.

| 순서 | PM 이슈 / 현재 보드 상태 | 이번 산출물 | 다음 수용 조건 |
|---|---|---|---|
| 1 | [#42 환경](https://github.com/xrlab-dau/CHOOGuard/issues/42) / 양쪽 In progress | 아래 D-01~D-09 사실·미결정 표, 정확 Editor·GPU·기존 빌드 확인 | HMD와 계정 운영 결정, 관리자 확인이 필요한 항목 |
| 2 | [#44 Windows 회귀](https://github.com/xrlab-dau/CHOOGuard/issues/44) / 양쪽 Blocked | 기존 native bootstrap 5개 시험 재실행, 추가 Git 한글 경로 오류 수정·회귀 시험 | 적용 독립 검토. Windows 미검증이라는 기존 Blocker 설명은 정정 후보 |
| 3 | [#55 개발 시작 인계](https://github.com/xrlab-dau/CHOOGuard/issues/55) / 양쪽 Review | 병합된 SHA 확인, 아래 공통 계약·확장 경계와 통합 순서 정리 | #69의 다른 개발자 재현, 계약/경계 최종 검토 |
| 4 | [#24 Unity 기준선](https://github.com/xrlab-dau/CHOOGuard/issues/24), [#36 자동 시험](https://github.com/xrlab-dau/CHOOGuard/issues/36) / 실행 보드 In progress | 현재 소스·실행본과 기존 Windows XML 결과 결속 재확인 | XR 기준선과 최종 후보의 요구사항별 검증 |

보드 정정 후보: PR #61은 양쪽 **Done**이지만 Blocker에는 아직 “PR 검토 대기 / 병합 미완료”가 남아 있다. 보드 README·#55의 `b3183e6` 등 이전 SHA도 과거 기록이다. 새 개발 시작점은 위 병합 SHA로 설명하되, 기존 학교 작업의 불변 입력 SHA를 바꿀 때는 해당 작업 범위의 영향 검증을 한다. 이번에는 원격 설명·상태를 수정하거나 이슈를 닫지 않았다.

팀원 [#69 checkout 재현](https://github.com/xrlab-dau/CHOOGuard/issues/69), [#85 베이크·렌더](https://github.com/xrlab-dau/CHOOGuard/issues/85), [#90 Windows 장시간 측정](https://github.com/xrlab-dau/CHOOGuard/issues/90)은 양쪽 Ready다. PM의 현재 설치 확인을 다른 개발자의 #69 재현이나 #90 전체 수용으로 계산하지 않는다. [#86 benchmark](https://github.com/xrlab-dau/CHOOGuard/issues/86)는 실행 프로필·공개 입력/라이선스·캐시 예산 대기이며 GPU 존재만으로 Ready가 되지 않는다. CV-01~06 코드는 별도 PM 작업을 유지한다.

## #42 D-01~D-09 결정 자료

원문 항목은 [요구사항 발견](../../_bmad-output/planning-artifacts/requirements-discovery-v1.md)의 §5.2다. 설치와 로그인 관측을 관리자 정책 승인으로 확대하지 않는다.

| 항목 / 결정 역할 | 확인한 사실 | 남은 결정 또는 확인 |
|---|---|---|
| D-01 OS / PM·관리자 | Windows 11 Home x64, 10.0.22631. 학교 PC라는 사용자 확인 | 이 PC의 결과를 다른 학교 PC에 일괄 적용하지 않음 |
| D-02 설치 권한 / 관리자 | 필요한 Git·Python·Unity·Blender가 현재 설치됨. 이전 설정 기록에 설치 결과가 있음 | 이후 도구별 설치 허용 범위·관리자 운영 정책은 별도 |
| D-03 네트워크 / PM·관리자 | 이 세션에서 GitHub 읽기와 Git fetch 성공 | 모델·검색·기관 목적지의 전송/접속 정책까지 확인한 것은 아님 |
| D-04 GPU·실행 위치 / MAP·PM | RTX 3070 8 GiB, RAM 약 64 GiB. 이번 실행 위치 school-pc | 대용량 benchmark는 입력·예산별 파일럿 필요. PM CV와 독립 학교 작업 경계 유지 |
| D-05 Unity 라이선스 / PM | 기존 Windows 임포트·빌드·시험 로그 존재, 설치 Editor 실행 가능 | 라이선스 유형·학교 계정 사용 조건의 책임자 확인은 미기록 |
| D-06 HMD / XR | 현재 프로젝트 결정에서 모델·runtime 미정 | 실제 기기·PC 연결 방식 확인 전 HMD 수용 보류, Desktop 개발 계속 |
| D-07 자료등급 / PM | 이번 작업은 저장소 소스·합성 자산과 정제 기록에 한정 | 기관 원본/TEAM_INTERNAL 저장 승인 미확인. 최신 AGENTS의 자료 정책을 적용 |
| D-08 로그인 / PM·관리자 | GitHub CLI가 PM으로 이미 인증되어 있고 자격 저장 방식은 keyring | Windows 계정 공용 여부·사용 종료 시 GitHub/Unity/브라우저 자격 정리·철회 운영은 미결정. 기존 자격을 임의 삭제하지 않음 |
| D-09 Unity 버전 / PM·UX | ProjectVersion과 실제 Editor 모두 6000.3.23f1, Windows 모듈/실행본 존재 | 버전 변경 시 팀 기준선·영향 검증 갱신 |

## #55 현재 공통 계약과 팀 확장 경계

이 표는 위 소스 SHA에서 확인한 인터페이스와 검토용 작업 경계다. 새 확장 디렉터리가 이미 구현됐거나 제품 통합이 최종 승인됐다는 뜻은 아니다. 공통 파일 변경은 PM 검토, 생성 자산 변경은 Builder·Blender 원본과 해당 시험을 동반한다.

| 경계 | 현재 근거 / 보존할 계약 | 팀 작업 후보와 통합 확인 |
|---|---|---|
| 입력·행동 | `Packages/com.xrlab.chooguard.foundation/Runtime/TrainingContracts.cs`: `TrainingAction`의 AttemptId/ScenarioVersion/RoleId/PreStateHash/ActionId/TargetAnchorId/Modality, `ITrainingActionHandler.Submit(in TrainingAction)` | #72/#89는 입력 어댑터와 fixture를 별도 모듈로 준비. VR/Desktop 행동 의미와 잘못된 상태·중복 시도 거부 시험 유지 |
| Anchor·Quest·팀·피드백 | `Runtime/ScenarioProfile.cs`, `Runtime/TrainingSession.cs`, `Runtime/VirtualTeamSimulator.cs`와 `foundation/anchors/synthetic-room.json`. 세션이 상태를 소유하고 `ITeamStateProvider.States`는 읽기 전용 | #58/#71은 독립 합성 fixture부터 제출. 공통 schema·역할/Anchor ID 변경은 PM이 검토. 설명형 피드백을 공식 점수로 바꾸지 않음 |
| 상시 세계·NPC | `Demo/Runtime/StationWorldSession.cs`: `Tick`, `Act(incidentId, expectedRevision, action, target)`, Trace/History. `DemoEvacuee.cs`가 인솔·집결을 담당 | #79~#82는 구역/portal 데이터와 조립 모듈을 나눔. 실제 상태와 관측 상태·revision·도착 검사를 보존. 이 API와 TrainingAction의 통합 XR 어댑터는 아직 수용하지 않음 |
| UI·Desktop | `Demo/Runtime/DemoInput.cs`는 internal Desktop 입력, `StationWorldController.cs`와 `DemoGameController.cs`가 현재 Unity 연결 경로 | #88 UI는 읽기 전용 표시·입력 전달부터 분리. 직접 상태 변경이나 입력 성공을 가정한 완료 처리 금지. internal 클래스는 외부 공개 API로 가정하지 않음 |
| 장면·자산 | `Demo/Editor/FoundationDemoSceneBuilder.cs`, `scripts/art/build_station_assets.py`, `foundation/art/asset-manifest.json`, `foundation/art/object-references.json` | #74~#82는 구역·asset ID별 원본/모듈/검수 출력 분리. 공유 station-kit.blend·manifest·Builder 통합은 한 writer와 PM 검토. `.unity/.prefab/.asset/.meta` 직접 YAML 편집 금지 |
| 독립 학교 실행 | [학교 인계](../../docs/context/school-pc-handoff-2026-09-08.md)의 `school/art-review/`, `school/benchmarks/`, `school/windows-validation/` 경계 | #85/#86/#90은 기존 입력 SHA·recipe·도구 버전·출력 hash·자원 측정과 정제 증거를 제출. 대형 결과·raw/weights는 Git 밖에 유지 |

각 이슈의 실제 수정 파일 목록을 먼저 정하고 `feature/<issue>-<slug>` 등 기존 Git Flow 브랜치에서 작업한다. 위 신규 모듈의 구체 경로는 이슈별 범위 기록에서 확정한다. `Packages/`, `ProjectSettings/`, 정책·schema와 공통 생성기는 PM 소유를 유지한다.

통합 순서는 W0 준비 결과 → #55 계약/재현 검토 → 한 경로 playable(#76/#77→#79와 #88) → 확장 구역(#78/#80/#81) → 전체 연결 #82 → #91 성능 → #92 인계다. 각각의 실제 선행 관계는 #68과 해당 이슈를 확인한다. 독립 학교 #85/#90 및 #86 준비는 #55 전체 완료나 PM CV 결과를 기다리지 않는다.

## 새 오류 수정과 검증 근거

`python scripts/ci/repository_policy.py`가 CP949로 Git의 UTF-8 한글 파일명을 디코딩하다 `UnicodeDecodeError`로 종료됐다. 별도 임시 Git 저장소의 한글·공백 파일명과 금지 확장자 fixture로 재현했다. 변경 파일 조회에는 Git이 파일명을 따옴표/escape로 출력하여 실제 파일 대신 잘못된 경로를 검사하는 문제도 있었다.

- 수정 전 새 시험 2개가 각각 오류/실패했다.
- `scripts/ci/repository_policy.py`에서 Git 출력 인코딩을 UTF-8로 명시하고 변경 파일 목록을 NUL 구분으로 읽는다. 경로 양끝을 `strip()`으로 잘라내지 않는다. 검사 정책과 금지 확장자 목록은 그대로다.
- 수정 후 CI 시험 **7/7 통과**, 실제 저장소 정책 **834개 추적 파일 / 위반 0**. 새 미추적 문서는 별도 후보 경로 검사로 확인한다.
- 이번 재실행: Foundation **30 통과/1 제외**, bootstrap **32 통과/5 제외**, context **15 통과/1 제외**. 제외는 Windows symlink 권한과 POSIX/bash 전용 시험이다. native PowerShell 시험 **5개는 전부 실행·통과**했다.
- 기존 [Windows receipt](2026-09-08-validation.json)의 소스 hash **59/59**, 실행본 hash **143/143**이 현재 파일과 일치한다. 원시 XML의 EditMode **117 통과/1 제외/0 실패**, PlayMode **16 통과/0 실패**를 읽어 확인했다. 이번 후속 작업에서 Unity 시험을 새로 실행한 것으로 계산하지 않는다.
- 게임·Builder·생성 자산은 이번 수정 범위에 없다. 기존 실행본 시작 로그에 Direct3D 초기화가 있으며 Exception/Error 표식은 없다. 수동 완주·장시간 profiling·HMD·현장 효과는 미검증이다.

근거와 변경 hash는 [후속 receipt](2026-09-08-pm-followup.json)에 기록한다. 원시 보드/실행 기록은 ignored 로컬 경로에 보존한다. 다음 검토자는 위 정책 검사 2개 회귀와 Windows 명령을 확인하고 #44의 적용 독립 검토, #55의 다른 개발자 재현을 수행한다. 현재 단계는 로컬 수정·검증이며 공개 게시·최종 수용·병합은 수행하지 않았다.
