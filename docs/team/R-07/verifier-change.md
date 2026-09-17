# R-07 검증기 정책 기준선·증거 결속 — 변경 제안 (candidate)

> **상태**: `candidate_draft` · phase=`candidate` · `canonicalWriteAllowed=false`. 승인·수용·완료 표기가 아니다.
> 정본 `scripts/bootstrap/**`는 **수정하지 않았다.** 아래 P1·P2는 제안 diff이며, 격리 worktree 복사본에만 적용해 실행했다. 정본 통합은 PM 직렬 통합 대상이다.
> 수치·해시·종료 코드는 모두 격리 worktree 실행 영수증(`docs/evidence/R-07/test-receipt-candidate-index.json`)의 관측값이다.

- 이슈: **#52 (R-07)** · 상위 #68 · 후행 #23 (`artifact:52:R-07-test-receipt:accept`)
- 기준: `develop` `4ac58dfbc8969d5ba94a38637577878b990c83ad`
- 입력: `artifact:16:M0-03-allowlist:candidate` (#16), `artifact:43:R-03-governance-diff:candidate` (#43), `artifact:44:R-04-test-plan:candidate` (#44)

## 1. 쓰기 범위

| 경로 | 내용 |
|---|---|
| `scripts/team/R-07/policy_baseline.py` | 실행 가능한 정책 기준선 대조기 (`artifact:52:R-07-fixtures:candidate`) |
| `scripts/team/R-07/test_policy_baseline.py` | 부정 시험 15개 |
| `docs/team/R-07/verifier-change.md` | 이 문서 (`artifact:52:R-07-verifier-change-proposal:candidate`) |
| `docs/team/R-07/proposals/P1-verify_toolchain-policy-baseline.diff` | 제안 P1 |
| `docs/team/R-07/proposals/P2-test_bootstrap_powershell-oem-shim.diff` | 제안 P2 |
| `docs/evidence/R-07/test-receipt-candidate-index.json` | 실행 영수증 index (`artifact:52:R-07-test-receipt:candidate`) |

`docs/evidence/R-07/test-receipt-accept-index.json`은 candidate 쓰기 범위 밖이라 쓰지 않았다.

## 2. 현재 검증기 동작 (`verify_toolchain.py` @ `4ac58df`, sha256 `cc4f77df…a16a1`)

| 입력 상태 | 소스 위치 | 현재 결과 |
|---|---|---|
| 필수 정책 glob(`REQUIRED_POLICY_GLOBS`, 83–88행)에 비어 있지 않은 파일이 하나도 없음 | `missing_required_policy` 190–199행 → `policy_hash_scope` 356행 | `[FAIL] policy_hash_scope`, exit 1 (R-04 N8) |
| 필수 glob 안의 **일부** 파일 삭제·비움 (다른 파일이 남음) | 같은 함수는 glob당 1개만 요구 | **감지 안 됨** |
| 비필수 정책 파일(`POLICY_GLOBS` 48–63행) 삭제·비움 | 324–328행은 존재하는 파일만 hash | **감지 안 됨** (receipt에서 키가 사라질 뿐) |
| 정책 파일 바이트 변경 (hash drift) | 324–328행 기록만, 비교 없음 | **감지 안 됨**. `required_ok`·exit 불변 (R-04 N7, 이번 E4c→E4e도 exit 1→1) |
| 정책 범위에 새 파일 추가 | 비교 없음 | **감지 안 됨** |
| `--write` 대상 경로 위반 | `receipt_target` 257–271행 | 검사 시작 전 exit 2 (R-04 N6a–h) |

즉 "누락 경로·빈 파일·정책 hash drift·대상 부재를 0이 아닌 종료로 거부"는 현재 검증기에서 **대상 경로(`--write`)와 필수 glob 전체 부재에만** 성립한다.

## 3. 기준선 대조기 `scripts/team/R-07/policy_baseline.py`

정본 검증기의 `POLICY_GLOBS`·`REQUIRED_POLICY_GLOBS`·`sha256`를 **import해 그대로 재사용**한다(복제 없음). 정본을 고치지 않고 지금 바로 실행할 수 있다.

| 명령 | 역할 |
|---|---|
| `build --root <checkout> --output <new.json> [--source-ref <sha>]` | 현재 정책 범위를 기준선으로 기록. 필수 glob 부재·빈 파일·빈 범위에서는 만들지 않음(exit 2). 기존 파일 덮어쓰기 금지 |
| `check --root <checkout> --baseline <b.json> [--receipt <verify_toolchain receipt>] [--output <new.json>]` | worktree와(선택) 검증기 영수증의 `policy_sha256`을 기준선과 대조 |
| `authority --root <checkout> --profile docs/team/M0-03/execution-profile.json` | 검증기 범위와 #16 `policySources`를 나란히 비교. 하나를 고르지 않음 |

판정 코드: `missing_path` · `empty_file` · `hash_drift` · `unexpected_file` · `required_glob_unmatched` · `scope_changed` · `path_case_changed`. 모든 발견을 한 번에 보고한다. 대소문자만 다른 경로는 `unexpected_file` 대신 `path_case_changed`로 보고하고 내용 hash는 그대로 비교한다. 목록 작성 뒤 사라진 파일은 예외 대신 `missing_path`다. 기준선·영수증·프로파일 JSON은 UTF-8 BOM(Windows PowerShell 5.1 기본 저장)이 있어도 읽는다.

종료 코드: **0** 일치(`state: ok`) · **1** 발견 있음(`state: cannot_proceed`) · **2** 대상 입력(root·baseline·receipt·profile·output) 부재·형식 오류·덮어쓰기 — 검사를 시작하지 않음. 출력에는 저장소 상대 경로와 고정 코드만 들어가고 절대 경로·파일 내용은 들어가지 않는다.

## 4. 제안 P1 — 검증기에 `--policy-baseline` 통합

`docs/team/R-07/proposals/P1-verify_toolchain-policy-baseline.diff` (sha256 `128f8bbacd5742006b0c106f565c633d1bc719d575da1c0d962c05a7f494a8a9`, §10 리뷰 후속 반영)

- `--policy-baseline <json>`을 주면 이미 계산한 `policy_sha256`을 기준선과 대조해 `checks.policy_baseline`에 기록하고 **필수 항목**(`required_checks`)에 추가한다. 옵션을 주지 않으면 기존 동작·영수증 형태가 그대로다.
- 기준선이 없거나 형식이 틀리면 `--write` 규칙과 같게 **검사를 시작하지 않고 exit 2**.

격리 복사본 실행 결과(E5):

| 경우 | 결과 |
|---|---|
| 기준선 파일 없음 / 형식 오류 | exit 2 / exit 2 |
| P1 적용 **전** 기준선으로 P1 검증기 실행 | `policy_baseline` FAIL: `hash_drift scripts/bootstrap/verify_toolchain.py` — 검증기 자체 변경도 정책 drift로 잡힘 |
| P1 적용 **후** 만든 기준선, 변경 없음 | `policy_baseline` ok (실패 필수 항목은 이 기기에 없는 pi·uv·pi_packages·research_venv뿐) |
| 같은 기준선, `AGENTS.md` 1줄 변경 | `policy_baseline` FAIL `hash_drift AGENTS.md`, 실패 필수 항목에 `policy_baseline` 추가 |
| 기존 bootstrap 시험 (P1만 적용) | P1 전과 동일: 42 / 35 통과 / 5 실패(F1) / 2 skip — 회귀 없음 |

**통합 절차 제안** (PM 결정 필요):

1. §6의 정책 권위를 먼저 확정한다.
2. P1 병합 커밋에서 `policy_baseline.py build`로 기준선을 만든다. 검증기·정책 파일 변경은 기준선을 반드시 무효화하므로(위 표 2행), **정책 파일을 바꾸는 PR은 같은 PR에서 기준선을 재생성하고 리드 검토를 받는** 규칙이 필요하다.
3. 기준선 파일은 그 자체가 정책 파일이다. 저장 위치(예: `POLICY_GLOBS`·`CODEOWNERS` 보호 범위 안)는 R-03 변경안 A와 함께 결정한다.
4. CI(`required-quality-gate.yml`)에서 `--policy-baseline`을 켤지는 별도 결정이다. 이 제안은 워크플로를 바꾸지 않는다.

## 5. 제안 P2 — PowerShell 시험 shim을 OEM 코드 페이지로 기록 (R-04 F1)

`docs/team/R-07/proposals/P2-test_bootstrap_powershell-oem-shim.diff` (sha256 `64522838f2f0822653d50274ba81cf72720615637512d72535b396578ad3b097`)

- 원인(R-04 F1): 시험이 `sys.executable`과 dispatcher 경로를 넣은 `.cmd` shim을 UTF-8로 쓰지만 cmd.exe는 OEM 코드 페이지로 읽는다. 사용자 폴더가 비ASCII인 Windows(코드 페이지 949)에서 PowerShell 시험 8개 중 5개가 경로 오류로 실패한다.
- 변경: shim을 `encoding="oem"`으로 쓰고, OEM 코드 페이지로 표현할 수 없는 경로면 실패 대신 skip.
- 실행: 같은 기기·같은 비ASCII 인터프리터 경로에서 **E1 42 / 35 / 5 실패 / 2 skip → E6(P1+P2) 42 / 40 / 0 실패 / 2 skip, exit 0**.
- 시험 클래스는 `os.name == "nt"`에서만 실행되므로 POSIX에는 영향이 없다(`oem` 코덱은 Windows 전용).

## 6. 정책 권위 비교 — 미해결, 두 출처 보존

`authority` 실행(E4n) 결과, `state: authority_unresolved`:

| 구분 | 개수 | 내용 |
|---|---|---|
| 둘 다 포함 | 4 | `.github/CODEOWNERS`, `.pi/settings.json`, `.pi/workflows.json`, `docs/adr/0006-official-unity-editor-mcp.md` |
| #16 `policySources`에만 | 6 | `NOTICE.md`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt`, `docs/choo-guard-ai-native-pipeline-v1.md`, `docs/choo-guard-execution-backlog-v1.md` |
| 검증기 `POLICY_GLOBS` 범위에만 | 44 | `AGENTS.md`, `.github/workflows/*.yml`, `scripts/ci/*.py`, `scripts/bootstrap/*`, 나머지 `docs/adr/*.md` 등 (전체 목록은 증거 index) |
| #16 `sha256AtRef`가 현재 worktree와 다름 | 4 | `Packages/manifest.json`, `Packages/packages-lock.json`, `docs/choo-guard-ai-native-pipeline-v1.md`, `docs/choo-guard-execution-backlog-v1.md` (#16 `drift` 기록과 일치) |

두 목록은 목적이 다르다(검증기: 도구·CI·정책 문서 변조 감지 / #16 프로파일: 실행 경계·버전 고정 근거). 이 후보는 **어느 쪽이 정책 기준선의 권위인지 정하지 않는다**(stop condition: 권위 충돌 미해결 시 차단·두 출처 보존). 기준선 대조기는 어떤 범위를 쓰든 동작하지만, 기본값은 현재 정본 검증기의 `POLICY_GLOBS`다.

## 7. 쓰기 범위·lock (R-04/R-07)

- R-04(#44)는 CLOSED이고 `scripts/bootstrap/**`를 수정하지 않았다. R-07도 수정하지 않았다. 착수 시점에 `scripts/bootstrap/**`를 수정하는 열린 PR·브랜치 없음.
- P1·P2를 정본에 반영하려면 `scripts/bootstrap/verify_toolchain.py`·`test_bootstrap_powershell.py`에 대한 **직렬 쓰기**가 필요하다. lock 소유자는 PM(또는 PM이 지정한 통합자)이며, 이 후보는 그 lock을 가정하지 않는다.

## 8. 수용 기준 대응

| 수용 기준 | 이 후보의 근거 | 상태 |
|---|---|---|
| 누락·빈 파일·정책 hash drift·대상 부재를 관측 가능한 nonzero/state로 거부 | 대조기: 시험 18/18(리뷰 후속 R2), 실제 worktree E4f–E4l (exit 1 ×4, exit 2 ×3). 검증기 통합: P1 격리 실행 E5 | 대조기는 충족. **정본 검증기 자체는 P1 통합 전까지 미충족** |
| source/test/result hash와 정확한 경로 기록 | 증거 index에 소스·diff·기준선·로그·영수증 sha256과 저장소 상대 경로 | 기록함 |
| R-04 OS 실행은 분리, 장소 정책 없음 | R-04 영수증은 입력으로만 참조. 이번 실행은 별도 run·label `local` | 분리함 |
| 실행 영수증·독립 검토 전 완료/시험/AAA 주장 없음 | 독립 검토 미실행. 이 문서는 후보 제안 | 주장 없음 |

## 9. 한계·미결정

1. 이 기기에는 pi·uv·research venv가 없어 검증기 exit가 원래 1이다. P1에서 `policy_baseline` 실패가 exit를 0→1로 바꾸는 장면은 관측하지 못했고, `required_checks` 실패 목록 변화로만 확인했다.
2. POSIX(`bootstrap.sh`)와 PowerShell 7에서는 실행하지 않았다.
3. 기준선 저장 위치·재생성 절차·CI 적용 여부·정책 권위는 PM 결정 사항이다(§4, §6).
4. `scripts/team/R-07` 시험은 CI 자동 발견 대상(`scripts/ci/tests`, `scripts/art`, `scripts/context`)이 아니다. 로컬 실행 영수증으로만 결속했다.
5. `docs/choo-guard-native-validation.md`(local_unpublished, qualification 없음)는 읽지 않았다.

## 10. 리뷰 후속 (2026-09-15, 자동 리뷰 에이전트 지적 반영)

실행: `r07-20260917T073804Z` (증거 index 두 번째 항목). 기준 worktree는 첫 실행과 같은 `4ac58df`.

| 지적 | 반영 | 확인 |
|---|---|---|
| `read_json`이 UTF-8 BOM을 거부해 PowerShell 5.1로 저장한 JSON이 `*_invalid` | `utf-8-sig`로 읽음 (대조기·P1 모두) | 시험 추가, BOM 기준선 `check` exit 0 (R4b), P1 검증기도 BOM 기준선 사용 (R5a) |
| 대소문자만 다른 파일명이 거짓 `unexpected_file` | `path_case_changed`로 보고하고 내용 hash는 계속 비교 (대조기·P1) | 시험 추가(worktree·영수증), 변이 R3a 검출 |
| P1 비교에 `required_glob_unmatched`가 없어 문서의 코드 목록과 불일치 | P1 `compare_policy_baseline`이 정본 `missing_required_policy`로 보고 | 필수 정책 파일 비움 → `empty_file` + `required_glob_unmatched` (R5b) |
| glob 뒤 stat/hash 사이 파일 삭제 시 traceback | 대조기는 `missing_path`, 기준선 생성은 `baseline_source_changed_during_build`(exit 2), P1은 `missing_path` | 시험 추가, 변이 R3b 검출 |

- 3차 리뷰: 대소문자만 다른 이름이 둘 있는 기준선이 같은 파일을 두 번 판정 → `validate_baseline`이 `baseline_invalid`로 거부(시험 추가).
- 대조기 시험 18/18, 변이 3종 모두 실패로 검출. P1 적용 bootstrap 시험은 첫 실행 E1과 같은 5개(R-04 F1)만 실패하고 새 실패는 없음(R5d).
- 대소문자만 다른 이름 변경은 이 Windows 기기에서만 실행했다. 대소문자 구분 파일 시스템에서는 실행하지 않았다.
- symlink 탈출 검사는 이 후보 범위 밖으로 남긴다(정본 `symlinks_outside_repo`가 별도 필수 항목).
