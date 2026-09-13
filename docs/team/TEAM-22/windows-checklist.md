# TEAM-22 기존 Foundation Windows 재현·자원 측정 체크리스트

작업 #90 · candidate 단계 · 2026-09-14 · 기기 `laptop-A`

이 문서는 기존 Foundation 기준선을 Windows에서 다시 실행할 때 확인할 항목과, 2026-09-14 laptop-A에서 실제로 실행한 결과를 함께 적는다. 수치의 원본과 digest는 [`docs/evidence/TEAM-22/windows-reproduction-candidate-index.json`](../../evidence/TEAM-22/windows-reproduction-candidate-index.json)에 있다. 이 결과는 candidate 제출이며 PM 수용이 아니다.

## 1. 기준선

| 항목 | 값 |
|---|---|
| 선택 입력 | `or:90:windows-baseline` → `artifact:69:historical-unchanged-checkout:accept` (#69 CLOSED) |
| 대상 커밋 | `e7a6bb7a7fff0c301c04f93aeea8850dbde34779` (LiveKit·NGO 추가 전, `com.unity.pipeline` 포함) |
| checkout | 작업 checkout과 분리한 새 git worktree, detached |
| LFS | 34개 materialized, pointer 0, `git lfs fsck` OK |
| Unity 실행 전 추적 파일 변경 | 0 |

다른 분기 `artifact:120:foundation-published-source:accept`는 #120이 수용 전이라 쓰지 않았다. 2026-09-08 학교 PC 수치(`school/windows-validation/2026-09-08-*`)는 결과로 쓰지 않았고, 같은 커밋의 계측 도구만 사용했다.

## 2. 기기 capability

| 항목 | laptop-A 실측 |
|---|---|
| OS | Windows 11 Education 10.0.26200 (build 26200) x64 |
| CPU / RAM | Intel Core Ultra 5 125H 14코어·18스레드 / 15.6 GiB |
| GPU / 그래픽 API | Intel Arc 내장 GPU (driver 32.0.101.8424) / Direct3D 12 level 12.2 |
| 화면 | 2880×1800 @120Hz, 측정 창 1280×720, vSync 1 |
| 전원 | AC 연결, 화면 끄기·절전 60분 |
| Unity | 6000.3.23f1 (`09d2ecc7fb28`), `C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe`, Authenticode Valid |
| Git / LFS | 2.48.1.windows.1 / 3.6.1 |

## 3. 재현 절차

`<CHECKOUT>`은 대상 커밋의 새 checkout, `<RUN>`은 매번 새로 만드는 저장소 밖 출력 폴더다.

1. `git worktree add --detach <CHECKOUT> e7a6bb7a7fff0c301c04f93aeea8850dbde34779` → `git lfs pull` → `git lfs fsck`
2. EditMode (작업 지시의 명령 그대로)
   `"<UNITY_EDITOR>" -batchmode -projectPath "<CHECKOUT>" -runTests -testPlatform EditMode -testResults "<RUN>/EditMode.xml" -logFile "<RUN>/EditMode.log"`
3. PlayMode: 같은 형식에 `-testPlatform PlayMode`
4. Desktop 빌드: `-batchmode -quit -executeMethod ChooGuard.Foundation.Demo.Editor.FoundationDemoSceneBuilder.BuildDesktopPlayerMenu`
   - 작업 지시에 적힌 `BuildDesktopPlayerBatch`는 이 커밋에 **없다.**
   - 이 메서드는 예외를 잡아 로그만 남기므로 **종료 코드가 항상 0**이다. `CHOOguard Windows desktop player built` 로그와 `Builds/FoundationDesktop/ChooGuardFoundation.exe`로 판정한다.
5. 실행본 시작: `ChooGuardFoundation.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720`, 창 모드
6. 계측 빌드
   - `school/windows-validation/SchoolWindowsValidationDriver.cs`를 `<CHECKOUT>/Assets/SchoolValidation/`에 복사한다.
   - **OS 메모리 읽기 수정이 필요하다** (5절 결함 1).
   - `-executeMethod SchoolWindowsValidationBuilder.Build --school-build-output <새 폴더>`로 빌드한다.
7. 측정
   - 실행: `SchoolValidation.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 --school-duration-seconds <초> --school-output <RUN>/profile`
   - **batchmode로 실행하지 않는다.**
   - 창을 최소화하지 않는다.
   - 같은 프로세스의 OS 메모리를 외부 도구로 1초마다 함께 기록한다.
8. `python school/windows-validation/summarize_profile.py <RUN>/profile --warmup 60`이 받아들이는지 확인한다.

## 4. 체크리스트와 2026-09-14 결과

| # | 항목 | 확인 방법 | 결과 |
|---|---|---|---|
| 1 | 대상 SHA·LFS·Editor/OS 기록 | 1·2절 | ✅ |
| 2 | EditMode | exit code, XML 합계, 실패·skip 사유 | ✅ exit 0, 118개 중 117 통과 / 0 실패 / 1 skip. skip은 `ReconstructionReviewTests.SourceAvailableBuildPreservesBoundsOneViewAndSerializedOriginCamera` — "Local reconstruction output is intentionally not Git-tracked." |
| 3 | PlayMode | exit code, XML 합계 | ✅ exit 0, 16/16 통과 |
| 4 | Desktop 빌드 | 성공 로그 + 실행 파일 + 파일별 hash | ✅ 성공 표식 확인, 156개 파일 약 107 MB, exe sha256 `82efd5a8…` |
| 5 | 실행본 시작 | 창 모드, 응답, 로그 예외 | ✅ 20초 동안 창 "CHOOguard Foundation" 응답, 작업 세트 743 MiB, 예외 없음 |
| 6 | 6가지 사건 조합 | 계측 실행의 `observedCases` | ✅ central·east·west × GuidanceOutage·PassageObstruction 6가지 모두 발생. **발생만 관측했고 대응 완료는 아니다** |
| 7 | 연속 2사건 | PlayMode `StationWorldPlayTests.RandomIncidentVariationsAndTwoConsecutiveEpisodesRecoverWithoutTeleporting` | ✅ PlayMode 통과. 실행본에서는 15근무 연속 사건 발생만 관측(복구 조작 없음) |
| 8 | 인솔·집결 | PlayMode `ThreeDrillsAcrossFiveRolesRequireOrderedObjectsAndSixWalkingFollowers`, `FollowerRoutesAroundWallAndWaitsOnlyAfterRecruitmentAndArrival`, `AssemblyGuidanceFollowsAcceptedActionAndResets` | ⚠️ PlayMode에서만 통과. 실행본 조작 확인은 **미실행** |
| 9 | 새 근무·종료 | 계측 `shifts`, 종료 코드; PlayMode `EveryRoleCanPlayAndRestartWithOnlyItsOwnPropCompleted` | ✅ 120초마다 새 근무 15회, 1800초에 completed로 종료(exit 0), PlayMode 통과 |
| 10 | 렌더링 프레임 증거 | `batchMode=false`, 렌더 콜백 ≥ 프레임의 90% | ✅ 213,141프레임 = 렌더 콜백 213,141 |
| 11 | 장시간 메모리·프레임 | 요약기 수용 + 구간 표 | ✅ 1800초 실행 수용 (아래 표) |
| 12 | OS 메모리 교차 검증 | 드라이버 값과 외부 `Get-Process` 값 비교 | ✅ 작업 세트 중앙값 698.0 vs 697.9 MiB, 전용 787.7 vs 787.6 MiB (비율 1.0001) |
| 13 | 수동 키보드·마우스 완주 | 사람이 실행본을 조작 | ❌ **미실행** — 사람이 필요 |
| 14 | HMD·새 멀티플레이·기관 수용 | — | 범위 밖 (결과를 확대하지 않음) |

### 장시간 측정 구간 (1800초, 워밍업 60초 이후 1,732개 샘플)

조건: 사용자가 노트북 앞에 있었고 가벼운 병행 사용을 허용했다. 창은 최소화하지 않았다.

| 구간 | fps 중앙값 / 최저 | p95 프레임 시간 최대 | 작업 세트 | 전용 메모리 | Unity 할당 |
|---|---|---|---|---|---|
| 60–360초 | 120.0 / 117.0 | 9.07 ms | 682.7–727.6 MiB | 774.5–796.5 MiB | 84.0–85.9 MiB |
| 360–660초 | 120.0 / 78.3 | 16.96 ms | 682.6–701.7 MiB | 774.9–793.4 MiB | 84.3–86.2 MiB |
| 660–960초 | 120.0 / 79.0 | 17.10 ms | 675.9–703.0 MiB | 767.2–794.5 MiB | 84.3–86.2 MiB |
| 960–1260초 | 120.0 / 71.5 | 17.08 ms | 683.2–707.6 MiB | 773.6–798.0 MiB | 84.4–86.3 MiB |
| 1260–1560초 | 120.0 / 67.9 | 17.09 ms | 681.5–709.2 MiB | 771.0–798.9 MiB | 84.4–86.3 MiB |
| 1560–1800초 | 120.0 / 85.3 | 16.97 ms | 692.9–710.1 MiB | 782.5–799.7 MiB | 84.4–86.1 MiB |

요약기 결과는 다음과 같다.
- **프레임:** 1초 단위 fps 중앙값 120.0, 최저 67.9. 33 ms를 넘은 프레임은 1개(최대 33.6 ms)였다.
- **메모리 최대치:** 작업 세트 727.6 MiB, 전용 메모리 799.7 MiB, Unity 할당 86.3 MiB.
- **추세:** 워밍업 이후 작업 세트가 −23.3 MiB 변했다.
- **해석:** 360초 이후 가끔 나온 70~85 fps 하락은 병행 사용 조건 때문일 수 있으며, 원인을 따로 분리하지 않았다.

## 5. 이번 실행에서 확인한 결함

1. **계측 드라이버의 OS 메모리가 0으로 기록된다.** Unity 6000.3.23f1 Mono Windows 플레이어에서 `System.Diagnostics.Process.WorkingSet64`와 `PrivateMemorySize64`가 항상 0이다.
   - **영향:** 수정하지 않은 드라이버의 180초 파일럿은 렌더링 조건을 모두 충족했지만 요약기가 `Memory counters are unavailable or invalid`로 거부했다. 180개 샘플 전부에서 두 값이 0이었다.
   - **조치:** 검증용 복사본만 `kernel32 K32GetProcessMemoryInfo`로 읽게 고쳤다. 저장소의 원본 드라이버는 수정하지 않았다(패치 digest는 index에 있다).
   - **검증:** 수정 뒤에는 외부 측정값과 일치했다.
2. **작업 지시의 빌드 메서드 이름이 기준선과 다르다.** 3절 4번에 적었다.
3. **빌드가 checkout을 바꾼다.**
   - PlayMode 시험: generated 머티리얼 16개를 다시 저장한다(`_EMISSION` keyword 추가).
   - Desktop 빌드: 장면 1개와 prefab 5개를 다시 생성한다.
   - 계측 빌드: `ProjectSettings.asset`의 제품 이름을 바꾼다.
   
   모두 격리 checkout에서만 일어났고 diff는 digest로 남겼다. **작업 checkout에서 빌드하지 않는다.**

## 6. 미실행·한계

- **수동 조작 완주:** 사람의 키보드·마우스 조작으로 실행본을 완주하는 확인과, 실행본에서의 인솔·집결·대응 완료 조작은 실행하지 않았다.
- **측정 대상:** 카메라 렌더 콜백과 CPU 프레임 간격을 쟀다. 하드웨어 표시 시점이나 GPU 단독 시간은 재지 않았다.
- **대표성:** 내장 GPU 노트북 한 대, 1280×720 창, 120 Hz 패널에서의 결과라 다른 하드웨어를 대표하지 않는다.
- **실행본 차이:** 계측 실행본에는 드라이버가 추가되고 제품 이름이 바뀌어 있어, 일반 Desktop 실행본과 완전히 같지 않다.
- **메모리 해석:** 30분 측정 한 번이다. 메모리 누수가 없다고 주장하지 않는다.
- **원시 기록:** XML, 로그, CSV, 패치는 laptop-A 저장소 밖에 보존하고 digest만 게시한다.
