# 기기 프로파일과 laptop-A Unity 실행 파일럿

관련 이슈: [#69](https://github.com/xrlab-dau/CHOOGuard/issues/69) · 전체 계획 [#68](https://github.com/xrlab-dau/CHOOGuard/issues/68) · 인계 기준 [#55](https://github.com/xrlab-dau/CHOOGuard/issues/55)

이 문서는 #69 완료 기준의 「기기별 CPU/RAM/GPU·VRAM/가용 디스크/Editor/HMD/로그인 유지 정책 표와 미확정 항목」을 채우고, 같은 작업 단위에서 수행한 laptop-A Unity 실행 파일럿을 기록한다. 관측 시점은 2026-09-09이다.

**DoD 완료 판정이 아니다.** 작성자와 다른 제공자·모델의 독립 검토 전이며 이슈 종료는 PM이 판단한다. 기기는 익명 라벨로만 표기하고 호스트명·계정·절대 경로는 기록하지 않는다.

## 출처 구분

아래의 「이 세션」과 실측·실행 서술은 **원 작성자의 2026-09-09 실행 세션**을 가리킨다. 후속 문서 검토에서 기기나 Unity를 재실행한 결과가 아니다. 관측 수치는 보존하되, 원자료를 재열람하지 못한 검토자가 이를 자신의 관측으로 승격하지 않는다.

| 라벨 | 이 문서의 값이 나온 방식 |
|---|---|
| `laptop-A` | 이 세션에서 직접 실측 |
| `school-pc` | 기존 PM 기록 인용. **이 세션에서 학교 PC를 관측하지 않았다** |

학교 PC 인용 출처는 [PM 설치·검증 기록](../../school/windows-validation/2026-09-08-pm-setup.md)과 [PM 후속 기록](../../school/windows-validation/2026-09-08-pm-followup.md)이다. 인용값의 관측 시점은 2026-09-08이며 이후 변동은 확인하지 않았다.

## 기기 사양표

| 항목 | `laptop-A` (개인 노트북) | `school-pc` | school-pc 출처 |
|---|---|---|---|
| CPU | Intel Core Ultra 5 125H — 14 cores / 18 threads | **미기록** | — |
| RAM | 15.6 GiB | 약 64 GiB | pm-setup |
| GPU | Intel Arc Graphics (내장), 드라이버 32.0.101.8424 | RTX 3070 | pm-setup |
| VRAM | Unity 보고 9099 MB / App VRAM Budget 8388 MB (공유 메모리) | 8192 MiB | pm-setup |
| 가용 디스크 | 44.9 GiB / 231.2 GiB (파일럿 후) | 최초 약 667 GiB | pm-setup |
| OS | Windows 11 Education 10.0.26200 | Windows 11 Home x64 | pm-setup |
| 그래픽 API | **Direct3D 12** [level 12.2] | **Direct3D 11** | pm-setup |
| Unity Editor | 6000.3.23f1 (rev `09d2ecc7fb28`) | 6000.3.23f1 (6000.3.15f1 병존) | pm-setup |
| Unity 빌드 모듈 | `windowsstandalonesupport` 설치됨 | Windows Mono 모듈 확인 | pm-setup |
| Unity Hub | 3.21.1 (machine 범위) | 설치 확인, **버전 미기록** | pm-setup |
| git | 2.48.1.windows.1 | 2.55.0.windows.5 | pm-setup |
| git-lfs | 3.6.1 | 3.7.1 | pm-setup |
| GitHub CLI | 2.100.0 | 2.100.0 | pm-setup |
| Python | 3.11.9 | 3.11.3 | pm-setup |
| Blender | 4.5.9 LTS | 4.5.9 LTS | pm-setup |
| OpenXR 런타임 | 등록 없음 | **미정** | pm-followup D-06 |
| HMD | 미탐지 | **미정** (`undecided_not_run`) | pm-followup D-06 |
| 로그인 유지 정책 | 개인 기기, 2026-09-09 사용자 Hub 로그인. 정책은 **미확정** | **미결정** (공유 PC 자격 보관·로그아웃 운영 결정 필요) | pm-setup |
| 심볼릭 링크 생성 권한 | 없음 (`WinError 1314`) | 없음 (Windows 권한) | pm-setup |

## 미확정 항목 — 측정과 결정의 구분

추가 측정으로 채울 수 있는 항목과, 사람이 정해야 채워지는 항목을 구분한다. 뒤쪽은 실기 실행을 해도 값이 나오지 않는다.

### 측정으로 채울 수 있는 것

- `school-pc`의 CPU 모델. 기존 기록에 없다. 학교 PC 담당자가 1회 조회하면 된다.
- `school-pc`의 현재 가용 디스크. 인용값은 2026-09-08 설치 이전 수치다.
- `school-pc`의 Unity Hub 버전.

### 결정이 필요한 것 (측정 대상 아님)

- HMD 기종과 연결 방식. 양쪽 모두 미정이다. 공통 시험 항목은 계획할 수 있으나, 기종별 연결·런타임·수용 조건 확정과 실기 실행은 기기 선택·확보 후에 한다.
- OpenXR 런타임 선택. HMD 결정에 종속된다.
- 공유 PC 로그인·자격 보관·로그아웃 운영 정책. `school-pc`는 공유 기기이므로 [ADR 0005](../adr/0005-school-pc-unity-workstation.md)의 D-08 검증과 관리자 확인이 선행한다.

`laptop-A`의 Hub 로그인은 개인 기기에 대한 사용자 결정이며, 공유 PC 정책을 해결한 것으로 취급하지 않는다. 조사 결과만으로 설치·로그인 공유를 승인하지 않는다.

## laptop-A Unity 실행 파일럿

기준 커밋 `106df22c9af28ec09bd515e68f72144f718cbaee`([PR #103](https://github.com/xrlab-dau/CHOOGuard/pull/103) head)에서 Unity 배치모드로 실행했다.

| 검사 | 결과 | 판정 근거 |
|---|---|---|
| 최초 임포트 + C# 컴파일 | 종료 코드 0, 664초 | `error CS####` 0건 |
| EditMode | 118개 중 **117 통과 / 0 실패 / 1 스킵** | 결과 XML `failed=0` |
| PlayMode | 16개 중 **16 통과 / 0 실패** | 결과 XML `failed=0` |
| Windows64 빌드 | **성공** | 성공 로그 + 실행본·`_Data`·`UnityPlayer.dll` 실재 확인 |

EditMode 스킵 1건은 `ReconstructionReviewTests.SourceAvailableBuildPreservesBoundsOneViewAndSerializedOriginCamera`이며 사유는 "Local reconstruction output is intentionally not Git-tracked"다. 학교 PC의 제외 사유와 같다.

Windows 빌드 판정에 종료 코드를 쓰지 않은 이유는 아래 결함 1을 참조한다.

### 증거 재열람 상태

위 표는 원 작성자의 실행 보고이며 **독립 재실행 PASS가 아니다**. 이 PR 문서에는 검토자가 재열람할 수 있는 laptop-A 정제 로그·XML·실행 명령·산출물 hash를 묶은 receipt 링크가 없다. 원자료가 없다는 뜻은 아니지만, Git 추적 밖 보존을 서술한 것만으로 독립 검수가 완료되지는 않는다.

독립 수용에는 원 작성자가 대상 SHA, 익명 기기 라벨, 시각·시간대, 정확한 명령과 종료 코드, 시험 총수·실패·스킵 사유, 정제 로그/XML 및 산출물·diff의 SHA256을 연결한 재열람 경로를 제공해야 한다. 호스트명·계정·개인 절대 경로·라이선스·자격은 제외한다. 후속 검토자는 원자료 대조 여부와 실제 재실행 여부를 따로 기록한다. 이 문서 수정으로 새로운 실측값이나 과거 원자료 hash를 만들어 넣지 않는다.

### 학교 PC 결과와의 대조

| 검사 | `school-pc` (2026-09-08) | `laptop-A` (2026-09-09) |
|---|---|---|
| EditMode | 118 중 117 통과 / 1 제외 | 118 중 117 통과 / 1 스킵 |
| PlayMode | 16개 모두 통과 | 16개 모두 통과 |
| Windows 빌드 | exit 0, 성공 로그·파일 확인 | 성공 로그·파일 확인 |
| 재생성 자산 | 22개 | 22개 (머티리얼 16 + 씬·프리팹 6) |

두 기록은 서로 다른 GPU·RAM과 **서로 다른 그래픽 API**(D3D11 / D3D12)에서 같은 시험 집계를 보고한다. 집계의 일치는 동일 소스·입력·생성물의 통제된 비교나 성능 동등성을 입증하지 않는다. 소스와 산출물 대응을 추가 대조해야 하며, 이 표는 학교 PC 실기·장시간 성능·HMD 수용을 대신하지 않는다.

### 작업 트리 취급

PlayMode가 머티리얼 16개를, 빌드가 씬·프리팹 6개를 재생성했다. 두 diff와 SHA256을 Git 추적 밖 보존 경로에 남긴 뒤 Git 기준으로 복원했고, 최종 작업 트리는 clean이다. `.unity`·`.prefab`·`.meta` YAML을 손으로 편집하지 않았다.

## 관측된 결함 2건

두 건은 아래 기준 커밋에서 보고된 결함이다. 후속 변경의 수용에는 해당 변경 범위의 재현·검증이 별도로 필요하다.

### 결함 1 — 기준 커밋에서의 관측: Windows 배치 전용 진입점 부재

`FoundationDemoSceneBuilder.BuildDesktopPlayerMenu`는 모든 예외를 `Debug.LogException`으로 삼킨다. 따라서 빌드가 실패해도 Unity 종료 코드는 0이 되고, 자동화는 실패를 놓친다. 같은 파일의 Mac 경로에는 "Batch entry point deliberately propagates failures to Unity's command-line exit status"라는 주석과 함께 실패를 전파하는 `BuildMacPlayerBatch`가 있으나, **Windows에는 대응 진입점이 없다.**

이 문서의 빌드 판정에 종료 코드를 쓰지 않고 성공 로그와 실제 파일을 확인한 이유가 이것이다. 학교 PC 기록도 같은 우회를 사용했다.

위 서술은 파일럿 SHA `106df22c9af28ec09bd515e68f72144f718cbaee`에 한정한다. [PR #108의 검토 대상 커밋](https://github.com/xrlab-dau/CHOOGuard/commit/de157b57e9e686bf1d180c5933c7c009e223673a)은 `BuildDesktopPlayerBatch`를 추가한다. 그 변경의 검증·수용은 별도이며, 이 참조가 병합이나 현재 HEAD의 검증 완료를 의미하지 않는다. 새 자동화는 예외를 잡는 메뉴가 아닌 배치 진입점을 명시해야 한다.

### 결함 2 — 씬 생성기가 바이트 단위로 재현되지 않는다

원 작성자는 빌드가 재생성한 6개 파일에서 41,536줄 삽입 / 41,536줄 삭제, 그중 `FoundationDemo.unity`에서 41,137줄 삽입 / 41,137줄 삭제를 보고했고 이를 `fileID` 변경으로 해석했다. 그러나 줄 수와 식별자 변경만으로 장면의 의미적 동등성을 증명할 수는 없다. 정제 diff/hash와 참조 정합성·직렬화 비교 근거를 재열람하지 못했으므로 **의미적 동등성은 독립 검증되지 않았다**. 바이트 단위 재현성 문제와 실제 장면 회귀 여부를 분리해 조사해야 하며, 이 문서는 큰 diff를 무조건 무의미한 변경으로 취급하지 않는다.

두 결함 모두 이 문서에서 수정하지 않았다. 코드 변경은 실패 시험 선행이 필요하며 별도 작업 단위에 속한다.

## Unity Hub 미탐지 조사 — 당시 원인은 미확정

[team01 checkout 기록](team01-local-checkout-2026-09-08.md)은 `laptop-A`의 Unity Hub를 "미탐지 — 설치 오류는 당시 추정, 원인 미확인"으로 기록하고, 설치 시도 여부와 미탐지 원인을 확정하지 않은 채 남겼다. 같은 문서의 검토 기록은 이 항목을 「해당 기기에서만 관측 가능한 단일 출처」로 분류했다. 이 절은 그 열린 항목에 관측을 보탠다.

2026-09-09 `laptop-A`에서 MSIX(Appx) 형식의 Unity Hub 3.21.1이 확인됐다. 패키지 폴더 생성 시각은 2026-09-08 18:44이다.

원 기록의 다섯 가지 검사만으로는 MSIX 설치 부재를 확정할 수 없다. 다만 **어떤 MSIX 설치도 이 검사에 탐지될 수 없다는 뜻은 아니다**. 검색 권한·범위, 패키지 manifest, 실행 방식과 가상화 설정을 확인하지 않았으므로 이번 미탐지에 작용한 경로는 미확정이다.

| 원 기록의 검사 | 해석의 한계와 보완 |
|---|---|
| `Program Files\Unity Hub` 및 사용자별 설치 경로 | 고정 경로 부재만으로 패키지 설치 부재를 판정하지 않는다. 패키지 등록 정보와 설치 위치를 함께 확인한다 |
| `Unity Hub.exe` 파일 검색 | 검색 범위·권한·오류를 기록한다. ACL 때문에 누락될 수 있으나, 이번 누락 원인으로 입증된 것은 아니다 |
| 레지스트리 Uninstall(HKLM·HKCU·WOW6432) | 이 키의 부재만으로 패키지 등록 부재를 판단하지 않는다. 기존 설치 흔적과 패키지 등록을 구분한다 |
| `%APPDATA%`·`%LOCALAPPDATA%`의 `UnityHub` 설정 폴더 | 가상화 예외와 기존 파일 쓰기 등으로 실제 경로가 달라질 수 있다. 모든 쓰기가 `LocalCache`로 간다고 단정하지 않는다 |
| 시작 메뉴의 `Unity Hub.lnk` | 특정 `.lnk`의 부재와 앱 등록 부재는 같지 않다. manifest와 실제 등록 정보를 함께 대조한다 |

[Microsoft의 MSIX 가상화 문서](https://learn.microsoft.com/en-us/windows/msix/desktop/flexible-virtualization)는 Windows 버전·manifest 설정에 따른 예외를 설명한다. 이 일반 설명은 해당 Unity Hub 패키지 설정의 직접 관측을 대신하지 않는다.

보완 조회인 [Get-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/get-appxpackage)의 **기본 조회는 현재 사용자 범위**이다. `-AllUsers` 또는 다른 사용자 조회에는 관리자 권한이 필요하므로 관리자 승인 범위에서만 수행한다. 결과에는 조회 범위·권한·시각과 실패 여부를 명시하고, 현재 사용자 결과가 비었다는 이유로 다른 사용자나 장치 전체의 미설치를 선언하지 않는다. 이 조회를 이번 문서 검토에서 새로 실행하지는 않았다.

**다만 2026-09-08 당시의 미탐지 원인은 여전히 확정하지 못한다.** 원 기록의 관측이 위 패키지 생성 시각보다 앞섰는지 뒤섰는지 확인할 수 없었기 때문이다. 원 기록의 「원인 미확인」 판정은 유지된다. 확정된 것은 탐지 방법의 한계이지 당시 설치 상태가 아니다.

2026-09-09 현재 `laptop-A`에는 사용자 결정으로 NSIS machine 범위 설치 1벌만 남겼다. 설치본은 공식 배포 URL에서 받아 SHA256 대조와 Unity Technologies 코드 서명 확인을 거쳤고 관리자 승인으로 설치했다. 사용자 경로 설치나 권한 우회를 사용하지 않았다. 이로써 원 기록의 미확정 항목 「Unity Hub 부재가 학교 인계 절차에 주는 영향」은 해소된다. Hub가 기존 Editor 6000.3.23f1을 인식하므로 인계 문서의 「Unity Hub에서 저장소 루트를 6000.3.23f1로 연다」 절차가 `laptop-A`에서 성립한다.

위 표의 Unity 보고 9099 MB와 App VRAM Budget 8388 MB는 원 작성자가 관측한 값이며, 전용 VRAM 용량이나 목표 작업의 지속 가능한 메모리 여유를 입증하지 않는다. [Microsoft DXGI 문서](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_4/ns-dxgi1_4-dxgi_query_video_memory_info)도 OS 제공 예산과 현재 사용량을 구분한다. 이 일반 정의만으로 Unity 로그의 계산 방식까지 확인한 것은 아니다. **목표 작업 부하에서의 실가용 VRAM은 여전히 미확정**이며, 내장 GPU 공유 메모리의 사용량·예산 변동·성능을 별도 측정해야 한다.

## 미수행·한계

- 학교 PC를 이 세션에서 관측하지 않았다. 표의 `school-pc` 값은 전부 2026-09-08 기존 기록의 인용이다.
- 수동 조작 완주, 장시간 FPS·메모리 프로파일, HMD 실기는 수행하지 않았다. HMD 미탐지와 OpenXR 등록 없음은 당시 조회 결과이며, 모든 장치·런타임의 절대적 부재를 입증하지 않는다. HMD 실기에는 기기·런타임 확보가 필요하지만 데스크톱 수동 완주와 성능 측정은 별도 미수행 항목이다.
- 무거운 모델링·렌더·베이크와 대용량 benchmark는 수행하지 않았다. `laptop-A`의 자원으로 학교 PC 배정 근거가 된 규모를 대신하지 않는다.
- 이 파일럿은 [ADR 0005](../adr/0005-school-pc-unity-workstation.md)의 「Windows 실행본·HMD 검증은 school-pc」 결정을 변경하지 않는다. 해당 결정의 갱신 여부는 PM 판단이다.
- `evidence.school_pc_setup_20260908`의 stale 상태는 이 문서에서 다루지 않았다. 학교 PC 담당자의 receipt 재열람이 필요하다.
- 심볼릭 링크 거부 경로는 양쪽 기기 모두 Windows 권한 제약으로 미검증이다.
- 원 작성자는 2026-09-09에 `laptop-A`의 Unity 라이선스를 ULF 단독, 2026-10-16 만료로 기록했다. 후속 검토에서 라이선스 상태를 조회하지 않았으며, 실제 실행 전 소유자가 유효성을 확인해야 한다. 라이선스 파일·계정 정보는 게시하지 않는다.
- 실행하지 않은 시험·실기를 PASS로 표시하지 않았다.
