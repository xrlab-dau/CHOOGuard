# TEAM-01 개인 노트북 checkout·경량 검증 기록

관련 이슈: [#69](https://github.com/xrlab-dau/CHOOGuard/issues/69) · 전체 계획 [#68](https://github.com/xrlab-dau/CHOOGuard/issues/68) · 인계 기준 [#55](https://github.com/xrlab-dau/CHOOGuard/issues/55)

팀원이 PM의 작업 디렉터리를 복사하지 않고 원격 소스만으로 개발을 시작할 수 있는지 한 차례 재현한 기록이다. **DoD 완료 판정이 아니며** 독립 검토와 PM 확인 전이다. 관측 시점은 2026-09-08이다.

- 기준 커밋: `e7a6bb7a7fff0c301c04f93aeea8850dbde34779` (`origin/develop`)
- 기기 라벨: `laptop-A` (Local). 호스트명·계정·절대 경로는 기록하지 않는다.
- 수행 범위: 이슈에 정의된 준비 범위. 설치·실제 자료·대용량 실행·Unity 실행은 수행하지 않았다.

## 새 checkout

원격에서 직접 clone했고 PM 작업 디렉터리를 복사·참조하지 않았다. clone 8초 / 14 MB, LFS 수신 후 21 MB. `git rev-parse HEAD`가 원격 `refs/heads/develop`과 일치함을 확인했다.

## 검사 결과

| 검사 | 종료 코드 | 결과 |
|---|---|---|
| `scripts/dev/check_foundation.py` (LFS 미수신) | 0 | 31 tests, 30 ok / 1 skip |
| `scripts/dev/check_foundation.py` (LFS 수신 후) | 0 | 31 tests, 30 ok / 1 skip |
| `scripts/context/context_graph.py validate` | 0 | 미해결 항목은 아래 참조 |
| `scripts/context/context_graph.py brief --topic handoff --machine local` | 0 | 정상 |
| `scripts/ci/repository_policy.py` | 0 | 849 파일, 위반 0 |
| `scripts/ci/pr_policy.py` (규격 입력) | 0 | 위반 0 |
| `scripts/ci/pr_policy.py` (음성 케이스) | 1 | 위반 7건 — 게이트 정상 작동 |

두 경량 검사 실행의 출력 차이는 임시 디렉터리명과 경과 시간뿐이므로 이 검사는 LFS 콘텐츠에 의존하지 않는다. 작업 트리 부작용은 없었다.

### 실행기 미설치·검사 실패·환경 제약 구분

- 실행기 미설치: 해당 없음. `python`·`python3` 모두 3.11.9로 확인했다.
- 검사 실패: 해당 없음. 종료 코드 0이다.
- 환경 제약 skip 1건: `test_repository_reader_rejects_symlinks_outside_repository`가 `WinError 1314`(심볼릭 링크 생성 권한 없음)로 skip됐다. 검사 실패가 아니며 이 경로는 `laptop-A`에서 미검증으로 남는다.

## LFS 선택 수신

`.gitattributes` LFS 패턴 10종, 추적 파일 34개(FBX 33 + Blender 1), 총 3.3 MB다. smudge를 끄면 전부 포인터로 유지되고 `git lfs pull`로 정상 수신됐다. 현 시점 develop 기준으로 불필요한 대용량 다운로드 위험은 확인되지 않았다. 자산 추가 시 재확인이 필요하다.

## 문서 링크

새 checkout에서 7개 문서(`README.md`, `docs/choo-guard-foundation-handoff.md`, `CONTRIBUTING.md`, `AGENTS.md`, `docs/ci.md`, `docs/context/README.md`, Demo `README.md`)의 상대 링크 37개를 검사했고 깨진 링크는 없었다.

보고서 저장 방식: 로컬에서 [`scripts/dev/check_foundation.py`](../../scripts/dev/check_foundation.py)를 실행하면 JSON 보고서를 표준출력으로 내보낸다. [Required Quality Gate](../../.github/workflows/required-quality-gate.yml)는 이 출력을 `foundation-data-report.json`으로 리디렉션해 저장하고 아티팩트로 업로드한다. 이는 [`docs/ci.md`](../ci.md)의 required quality workflow 설명과 일치한다.

## 인계 경로 모의 재현

노트북 구간의 브랜치·PR 게이트를 로컬에서 확인했다. 원격 push·PR 생성·PM 브랜치 접근은 하지 않았다. 음성 케이스가 실제로 거부되므로 게이트 통과는 공회전이 아니다.

미재현 구간은 학교 PC 실행 → 출력 hash·실제 자원량 → PR이다. `laptop-A`에서 수행할 수 없으며 학교 PC 담당자의 별도 수행이 필요하다.

## 관측된 미해결 항목

### 기준 SHA `b3183e6`와 develop의 관계

여러 TEAM 이슈가 독립 학교 실험 입력 기준으로 `b3183e61df95fee6d1afa0299ef97c7eacc1cec0`을 지정한다. 새 checkout에서 확인한 결과는 다음과 같다.

| 확인 항목 | 결과 |
|---|---|
| 포함 브랜치 | `origin/feature/foundation-starter` |
| `origin/develop`의 조상인가 | 아니오 |
| 공통 조상 | `196981f` (#53) |
| 분기 규모 | `b3183e6` 쪽 전용 23 커밋 / `origin/develop` 쪽 전용 3 커밋 |

`#55`는 「새 작업은 현재 `origin/develop`의 불변 SHA를 기록하고 이슈별 브랜치에서 시작한다」와 「과거 `b3183e6` 등 특정 SHA를 지정한 실험의 재현 조건은 유지한다」를 함께 규정한다. 따라서 `b3183e6`가 develop 계보 밖인 것은 그 자체로 오류가 아니라 실험 재현용 고정점으로 이해한다.

남는 확인 사항은 `#83`·`#84`·`#86`이 해당 SHA를 입력 기준으로 사용할 때 그 결과를 develop 기준 브랜치로 되돌릴 시점의 차이를 어떻게 다룰지에 대한 판단이다.

### context graph stale 노드

`scripts/context/context_graph.py validate`는 종료 코드 0이지만 `evidence.school_pc_setup_20260908`을 `stale; evidence=receipt_changed_or_missing`으로 보고한다. 선언 source를 실제 파일과 대조한 결과는 다음과 같다.

| 선언 source | 판정 |
|---|---|
| `school/windows-validation/2026-09-08-pm-setup.md` | 해시 일치 |
| `school/windows-validation/2026-09-08-validation.json` | 선언값과 실제 파일 해시 불일치 |

이 노드는 `authority: execution_receipt`, `topics: [runtime, handoff]`로 선언된 학교 PC Windows 검증 증거이며 coverage receipt이 `2026-09-08-validation.json`의 `sourceSha256` 필드를 가리킨다. `AGENTS.md`는 해시 변경을 재열람 대상으로 규정하고 자동 갱신 PASS로 보지 않으므로, 이 증거를 근거로 학교 인계 경로가 검증됐다고 간주하지 않았다. `docs/context/`의 기존 파일은 수정하지 않았다.

같은 실행에서 `source_drift` 5건(`open_world_20260907`, `realism_context_20260907`, `object_references_20260907`, `reconstruction_20260907`, `sequence_pilot_20260908`)과 폐기 결정 관련 `stale` 3건(`decision.assets.varco`, `decision.reconstruction.reusable_cv`, `decision.visual.flat`)이 함께 보고된다. 뒤 3건은 의도된 표기로 보이나 확인이 필요하다.

## 기기 사양표

| 항목 | `laptop-A` (개인 노트북) | 학교 PC |
|---|---|---|
| CPU | Intel Core Ultra 5 125H — 14 cores / 18 threads | 미관측 |
| RAM | 15.6 GiB | 미관측 |
| GPU | Intel Arc Graphics (내장) | 미관측 |
| VRAM | 보고값 2048 MiB — 내장 GPU 공유 메모리로 실가용량 미확정 | 미관측 |
| 가용 디스크 | 47.1 GiB / 231.2 GiB | 미관측 |
| OS | Windows 11 Education 10.0.26200 | 미관측 |
| git | 2.48.1.windows.1 | 미관측 |
| git-lfs | 3.6.1 | 미관측 |
| Python | 3.11.9 (`python`·`python3` 동일), py launcher 3.12.4 | 미관측 |
| Unity Editor | 6000.3.23f1 (rev `09d2ecc7fb28`) — `ProjectSettings/ProjectVersion.txt` 요구 버전과 일치 | 기존 기록상 설치 확인, 본 작업에서 미관측 |
| Unity Hub | 미설치 — 설치 오류 정황, 아래 참조 | 미관측 |
| OpenXR 런타임 | 등록 없음 | 미관측 |
| HMD | 미탐지 | 미관측 |
| 심볼릭 링크 권한 | 없음 (`WinError 1314`) | 미관측 |
| 로그인 유지 정책 | 미확정 | 미확정 |

### Unity Hub 미설치 경위

관측 시점에 `laptop-A`의 Unity Hub는 설치돼 있지 않았다. 의도된 미설치가 아니라 설치 과정에서 오류가 발생한 것으로 보인다. 확인 근거는 다음과 같다.

- `C:\Program Files\Unity Hub\Unity Hub.exe` 및 사용자별 설치 경로 3곳에 없음
- `Unity Hub.exe` 파일 검색 0건
- 레지스트리 Uninstall(HKLM·HKCU·WOW6432)에 `Unity 6000.3.23f1`만 등록, Unity Hub 항목 없음
- `%APPDATA%\UnityHub`·`%LOCALAPPDATA%\UnityHub` 설정 폴더 둘 다 없음
- 시작 메뉴에 `Unity.lnk`만 존재, `Unity Hub.lnk` 없음

`C:\Program Files\Unity\Hub\` 경로는 존재하나 그 아래에는 `Editor\6000.3.23f1`만 있고 Hub 실행 파일은 없다. 이 경로는 Unity Hub가 에디터를 설치할 때 쓰는 기본 경로 규칙이며 에디터 단독 설치 관리자도 동일한 규칙을 사용하므로, 이 폴더의 존재는 Hub 설치의 근거가 되지 않는다. Editor는 정상 설치된 반면 Hub만 누락된 상태다.

`docs/choo-guard-foundation-handoff.md`는 「Unity Hub에서 저장소 루트를 6000.3.23f1로 열고」를 지시하므로, Hub 없이 Editor 단독으로 이 절차를 수행할 수 있는지는 현재 미검증이다. Hub 재설치 후 재확인이 필요하며, 확인되지 않은 대안 절차를 문서화하지 않는다.

### 미확정 항목

- `laptop-A`의 실가용 VRAM. 내장 GPU 공유 메모리 구조상 보고값을 신뢰할 수 없다.
- 학교 PC 사양 전체. `laptop-A`에서 관측할 수 없으며 학교 PC 담당자의 기록이 필요하다.
- HMD 기종과 OpenXR 런타임. 양쪽 모두 미확정이다.
- 로그인 유지·계정 공유 정책. 조사 결과만으로 설치·로그인 공유를 승인하지 않는다.
- Unity Hub 부재가 학교 인계 절차에 주는 영향.

## 미수행·한계

- Unity Editor 실행, C# 컴파일, EditMode/PlayMode, 씬 검증은 수행하지 않았다. 본 이슈 범위 밖이다.
- Windows 실행본 빌드와 HMD 검증은 수행하지 않았다. 학교 PC 대상이다.
- `school/windows-validation/` 산출물의 실제 재현은 수행하지 않았다.
- 심볼릭 링크 거부 경로는 환경 제약으로 미검증이다.
- 작성자와 다른 제공자·모델의 독립 검토는 완료되지 않았다.
- 실행하지 않은 시험·실기를 PASS로 표시하지 않았다.
