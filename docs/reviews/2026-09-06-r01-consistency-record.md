# R-01 문서 선행관계·검토 지적 정합성 확인 기록

- 날짜: 2026-09-06.
- 작업 단위: R-01 (이슈 #41). 상위 단위 M0-01 (#10).
- 유형: 사람이 지시한 문서 대조·재현 관측 기록이다. 독립 검토 승인 영수증이 아니며 어떤 실행·병합 승인도 생성하지 않는다.
- 기준 커밋: `6798032c839336f2c059a81db12a60602e731eae` (develop).
- 작성 제공자: anthropic. 이 기록에 대한 다른 제공자의 독립 재검토는 미완료다.

이 문서는 세 가지만 한다. (1) 백로그와 작업 그래프의 선행관계 차이를 대조해 PM 결정 대상으로 올린다. (2) 105개 지적을 `bundle/lens/id`를 보존한 채 재분류한다. (3) 과거 "실행 미확보" 표현을 실제 회귀 실행 결과와 대조한다. 어떤 기준선 문서도 이 기록만으로 변경하지 않는다.

## 0. 불변 대상 manifest

독립 재검토는 아래 해시에 고정된 내용에 대해서만 유효하다. 파일이 바뀌면 새 manifest로 재검토한다.

| SHA-256 | 경로 |
|---|---|
| `5a5f3b0d18bbbee5bfcdfe15a2a693a0e004030fe7a5a94ea05b3676484e5738` | `specs/analysis-2026-09-06.md` |
| `91bd9212873c59134229dc0bd87cb25a3f7beea1538e2458823ba652e5929132` | `docs/choo-guard-work-unit-graph-v1.md` |
| `736f61cb1a2f19745efce69c4dd2ae01b1458c3e839f7147238a9dbb7a26de0c` | `docs/choo-guard-execution-backlog-v1.md` |
| `8d2378c7c59f55b5e80494e423f79ddb33c9b58f26485f33471539f6e8e4746f` | `docs/choo-guard-school-pc-bootstrap-v1.md` |
| `2303a7c2b9a961c5b3d2fce44cd40ea2c4beedb0926b9b1ee8a4a4049cb96b03` | `docs/reviews/2026-09-06-review-dispositions.md` |
| `1008db6d6b0e860fba081c295d9c685a877c4f19b1ba9ed7290593360203dea2` | `docs/reviews/2026-09-06-workflow-status.json` |
| `4bb8b4bbc846677963b4f391ef02f100d77e372f3a9bdc2580a36fb324a86413` | `scripts/bootstrap/verify_toolchain.py` |
| `aebb933d30bfab17d3b01454a2303251654cca9f9c9a7ba36361862fcb43538e` | `scripts/bootstrap/test_verify_toolchain.py` |
| `2f010cd145624ce013c1d6f072534b0b594336bca662ea808df2eb60cb2bbd22` | `scripts/bootstrap/bootstrap.sh` |
| `936dbd364eb597160d11de86756752f877013699b7e497a9d3fda64362bee006` | `scripts/bootstrap/bootstrap.ps1` |

이 기록 파일 자체는 검토 대상이며 병합 시점 blob 해시로 별도 고정한다. 최초 manifest 부재를 사후 해시로 소급 보완하지 않는다.

## 1. 선행관계 차이와 PM 결정 대상

백로그 v1.3 `§5~§8`의 `선행` 항목과 작업 그래프 v1.0 `§1` mermaid 간선을 단위별로 대조했다. 이슈 #41 수용 기준이 지목한 여섯 단위 모두에서 차이가 확인된다.

| 단위 | 백로그 v1.3 선행 | 그래프 v1.0 간선 | 차이 유형 |
|---|---|---|---|
| M1-05 | M0-00, M0-03 | `M1-02 --> M1-05` | 상호 배타. 양쪽이 서로의 선행을 갖지 않음 |
| M1-06 | M1-01, M1-02 | `M1-01 --> M1-06` | 그래프가 M1-02 간선 누락 |
| M1-07 | M0-00, M0-03, M1-02 | `M1-02 --> M1-07` | 그래프가 M0-00·M0-03 간선 누락 |
| M2-01 | M1-02, M1-07 | `M1-01 --> M2-01` | 상호 배타. 선행 노드 집합이 겹치지 않음 |
| M2-04 | M2-02, M4-01 | `M2-03 --> M2-04` | 상호 배타. 선행 노드 집합이 겹치지 않음 |
| M4-01 | 없음 (상태 `Ready`) | `M2-02 --> M4-01` | 그래프가 백로그에 없는 선행을 추가 |

관측 사실 세 가지를 구분한다.

- 백로그는 승인된 기준선이다(PR #11). 그래프 v1.0은 백로그에서 파생한 문서이며 자체 머리말에 "예정 의존성이지 실행·승인 완료 기록이 아니다"라고 적혀 있다. 따라서 충돌 시 백로그가 상위다.
- 그래프의 추가 간선 중 M2-01의 `M1-01`, M1-05의 `M1-02`, M1-06·M1-07의 각 간선은 백로그의 누락이 아니라 그래프가 새로 주장한 관계다. 근거는 그래프 문서에 기록되어 있지 않다.
- M4-01 차이는 현재 이슈 #32가 `Blocked`인 유일한 근거다. 다른 다섯 단위는 어느 해석을 택해도 현재 상태(`Blocked`)가 바뀌지 않는다.

### 1.1 PM 결정 제안

기본 원칙은 **두 문서의 합집합을 채택**한다. 선행을 늘리는 방향은 안전 측이며 승인된 백로그를 축소하지 않는다.

| 단위 | 제안 선행 | 근거 |
|---|---|---|
| M1-05 | M0-00, M0-03, M1-02 | 합집합. 실행 기록 manifest는 Permission Gate 경계가 정해진 뒤라야 기록 대상이 확정된다 |
| M1-06 | M1-01, M1-02 | 백로그 유지. 그래프에 M1-02 간선 추가 |
| M1-07 | M0-00, M0-03, M1-02 | 백로그 유지. 그래프에 두 간선 추가 |
| M2-01 | M1-01, M1-02, M1-07 | 합집합. M2-01은 school-pc 노드이며 Pi 세션 계약(M1-01) 없이는 실행 주체가 정해지지 않는다 |
| M2-04 | M2-02, M4-01 | 백로그 유지. M2-03도 M2-02·M4-01을 선행으로 두므로 그래프 간선은 중복이며 병렬 착수를 불필요하게 막는다 |
| M4-01 | **PM 판단 필요** | 아래 참조 |

M4-01은 두 해석의 결과가 실제로 갈리므로 별도 결정이 필요하다.

- **안 A — 백로그 유지(`Ready`, 선행 없음).** M4-01의 DoD는 `provisional 문구 포함`을 명시적으로 요구하는 임시 fixture 정의이며 책임 역할은 QA다. fixture를 소비하는 M2-03·M2-04는 이미 M2-02를 선행으로 갖고 있으므로 계약 정합성은 하류에서 강제된다. 이슈 #32가 즉시 착수 가능해진다.
- **안 B — 그래프 유지(M2-02 선행).** fixture 행이 요구하는 `행동 ID`·`대상 Anchor`·`기대 Quest 상태`는 M2-02의 TrainingAction 계약에서 나온다. 계약 확정 전 정의하면 재작업이 생긴다.

제안은 **안 A**다. 다만 fixture의 행동 ID·Anchor 열은 M2-02 확정 시 재검증 대상으로 표시하는 조건을 붙인다.

**결정 기록:** _(PM 결정 대기. 결정 전 백로그·그래프 어느 문서도 변경하지 않는다.)_

## 2. 105개 지적 정합성 재분류

### 2.1 구조 검증

`docs/reviews/2026-09-06-workflow-status.json`의 집계를 처리 기록 본문에서 독립적으로 재계산했다.

| 번들 | spec | safety | adversarial | 소계 |
|---|---|---|---|---|
| 요구사항 발견과 명세 001·002 | 8 | 8 | 6 | 22 |
| 촬영 질문과 도구 허용목록 | 8 | 8 | 8 | 24 |
| ADR과 기술 조사 | 8 | 8 | 5 | 21 |
| Pi 개발 명세와 설정 | **관점 실패(0)** | 8 | 6 | 14 |
| 부트스트랩·거버넌스 | 8 | 8 | 8 | 24 |
| 합계 | 32 | 40 | 33 | **105** |

- 리뷰어 수 검증: 5 번들 × 3 관점 = 15. 그중 1개(Pi 개발 명세·spec)가 `Prompt is too long`으로 실패했으므로 완료 리뷰어는 14다. `counts.reviewers_completed = 14`, `reviewers_failed = 1`과 일치한다.
- 지적 수 검증: 105는 `counts.findings_in_journal`과 일치한다. 실패한 관점은 지적 0건을 기여하며 지적 수에 포함되지 않는다.
- 처리 기록의 표 행 수는 97이다(지적 행 96 + 관점 실패 행 1). 지적 105건과 다른 이유는 같은 처리군에 묶인 다중 ID 행(예: `spec SPC-2, SPC-3`) 때문이며, 각 ID의 출처는 행 안에 보존되어 있다. 원본 지적을 삭제·병합한 흔적은 없다.

### 2.2 상태 구분

수용 기준은 "기존 지적이 전부 미해결이라고 단정하지 않는다"를 요구한다. 아래 세 축을 분리한다.

- **축 1 — 문서 반영:** 처리 기록에 정정 내용이 있는가. 대부분의 지적이 여기에 해당하나 이는 컨트롤러의 자체 서술이며 리뷰어 판정이 아니다.
- **축 2 — 실행 증거:** 실제 실행된 테스트·명령 결과가 있는가. 아래 2.3이 이 축에서 상태가 바뀐 지적을 특정한다.
- **축 3 — 독립 재검토:** `counts.independent_rereviews_completed = 0`. **105건 전부 이 축에서 미종결이다.** 축 1·2가 충족되어도 종결이 아니다.

따라서 "전부 미해결"도 "상당수 해결"도 정확하지 않다. 정확한 진술은 다음과 같다. **12건이 축 2에서 미해결에서 해결로 바뀌었고, 105건 전부가 축 3에서 미종결이다.**

### 2.3 실행 증거가 확보된 지적 (축 2 상태 변경)

`python3 -m unittest discover -s scripts/bootstrap -p 'test_*.py'` 를 이 기록 작성 시점에 재실행했다. 결과 `Ran 16 tests ... OK`, 종료 코드 0. 처리 기록의 "실행 차단"·"시험 미완료" 표현이 아래 지적에 대해서는 더 이상 현재 상태가 아니다.

| bundle | lens | id | 처리 기록의 과거 표현 | 실행 증거 (테스트 이름) |
|---|---|---|---|---|
| 요구사항 발견과 명세 001·002 | safety | SAF-6 | 명세 반영, 코드 시험 미완료 | `test_capture_extensions_are_rejected` |
| 요구사항 발견과 명세 001·002 | safety | SAF-7 | 단일 POLICY_GLOBS 요구. 생성기 미수정 | `verify_toolchain.py:48` `POLICY_GLOBS` 단일 출처 도입, `test_required_hash_scope_covers_governance`. 이 테스트는 거버넌스 경로가 범위에 선언됐는지만 확인하며 해시 산출 결과의 완전성은 검증하지 않는다 |
| Pi 개발 명세와 설정 | adversarial | ADV-5 | 명세·회귀 테스트 요구, 보호 CI 미변경 | `test_env_variants_are_rejected` (보호 CI 변경은 여전히 미실행) |
| 부트스트랩·거버넌스 | safety | SAF-2 | gate 전 원격 자동 설치. 운영 보류 | `test_sync_requires_explicit_install_approval` |
| 부트스트랩·거버넌스 | safety | SAF-3 | 합성 회귀 테스트 작성, 실행 차단 | `test_receipt_cannot_escape_repository`, `test_receipt_cannot_overwrite_policy`, `test_existing_receipt_is_append_only` |
| 부트스트랩·거버넌스 | safety | SAF-4 | 테스트 작성, 실행 차단 | `test_env_variants_are_rejected`, `test_env_example_is_not_a_secret_file` |
| 부트스트랩·거버넌스 | safety | SAF-5 | uv/환경/exit 테스트 작성, 실행 차단 | `test_missing_uv_fails_strict`, `test_missing_research_environment_fails_strict`, `test_failures_are_nonzero_without_strict_flag` |
| 부트스트랩·거버넌스 | safety | SAF-6 | 범위 테스트 작성, 실행 차단 | `test_required_hash_scope_covers_governance` |
| 부트스트랩·거버넌스 | safety | SAF-7 | 패키지 루트 링크 테스트 작성, 실행 차단 | `test_package_tree_link_cannot_escape` — **부분만 해소.** venv·OS 범위 보완은 미완료 |
| 부트스트랩·거버넌스 | safety | SAF-8 | 허용값 요구, 실제 parser·시험 보완 미완료 | parser 해소: `verify_toolchain.py:38` `MACHINE_LABELS`와 `argparse choices`. 시험은 `test_missing_machine_label_stops_before_any_tool_call`로 누락만 검증하며 **허용값 밖 라벨의 전용 시험은 없다.** 부분만 해소 |
| 부트스트랩·거버넌스 | spec | SPC-5 | 회귀 테스트 RED 미확보, 생산 코드 미수정 | `test_failed_command_output_is_not_success`. RED는 `workflow-status.json`의 `red_runs` 3회에 기록됨 |
| 부트스트랩·거버넌스 | spec | SPC-6 | 재귀 검사 요구, 코드·시험 미완료 | `test_nested_disallowed_settings_keys_are_rejected` |

`test_tracked_listing_failure_is_not_clean`은 컨트롤러가 RED 2차에서 추가한 보충 테스트이며 특정 원본 지적 ID에 귀속시키지 않는다. 원본 105건에 새 ID를 추가하지 않는다.

### 2.4 실행 증거가 없는 지적 (축 2 미해결 유지)

나머지 93건은 처리 기록에 적힌 상태를 그대로 유지한다. 실행이 확보되지 않은 대표 항목은 다음과 같다.

- **사람 승인으로만 해소 가능(문서로 대체 불가):** 요구사항 번들 `safety SAF-1`(보호 소유권), `spec SPC-6`(last-match CODEOWNERS), ADR 번들 `safety SAF-4`(정책·증거 소유권), Pi 번들 `adversarial ADV-3`·`ADV-6`(보호 경로 변경). 현재 `.github/CODEOWNERS`에 `docs/`·`specs/`·`.pi/`·`docs/evidence/` 항목이 없음을 확인했다. R-03 (#43)의 결정 대상이다.
- **Windows 미검증 유지:** 부트스트랩 번들 `adversarial ADV-5`(PowerShell native exitcode). `bootstrap.ps1`은 `exit $LASTEXITCODE` 전파로 수정됐으나 이 머신에 pwsh가 없어 실행 검증하지 않았다. R-04 (#44)의 대상이며 위 2.3의 해소 목록에 넣지 않는다.
- **운영 보류 유지:** Pi 개발 명세 번들 `safety SAF-2`(자유 tests 문자열 셸 실행), `adversarial ADV-1`(미승인 verifier 진입), ADR 번들 `spec SPC-5`(총 라운드 불일치). 해당 YAML은 변경하지 않았다.
- **조사 하네스 미수정:** ADR 번들 `safety SAF-2`·`SAF-3`, `adversarial ADV-1`. `tools/research/research.py`의 자유 `--model`·`--topic`·출력 경로 결함은 그대로다. R-06 (#46)의 대상이다.

## 3. 과거 미검증 표현과 최신 회귀 기록 대조

수용 기준 3번이 지목한 두 위치를 대조했다.

### 3.1 `specs/analysis-2026-09-06.md` F-13

현재 문구는 "합성 회귀 테스트 작성. 안전 판정 서비스 장애로 실행 미완료, RED/GREEN 미확보"다.

이 문구는 **낡았다.** `workflow-status.json`의 `controller_followup.test_execution`은 RED 3회(12건 중 11실패 / 14건 중 13실패 / 16건 중 2실패)와 GREEN 2회(14/14, 16/16)를 기록하고 있으며, 본 기록 작성 시점에 16/16 GREEN을 재현했다. F-13이 지목한 다섯 결함(경로 이탈·덮어쓰기·실패 종료·필수 검사 누락·중첩 설정 키)은 2.3의 테스트로 각각 덮인다.

정정 제안 문구: "합성 회귀 테스트 16건 GREEN(RED 3회 선행)으로 경로 이탈·덮어쓰기·실패 종료·필수 검사·중첩 키를 덮었다. Windows(pwsh) 실행 검증과 수정본의 독립 재검토는 미완료다." — **PM 승인 전 적용하지 않는다.**

### 3.2 `docs/choo-guard-school-pc-bootstrap-v1.md`

| 위치 | 현재 문구 | 판정 |
|---|---|---|
| 39행 | "`verify_toolchain.py`의 `--write`는 경로 이탈과 덮어쓰기 방지가 검증되지 않았다" | **낡음.** `test_receipt_cannot_escape_repository`·`test_receipt_cannot_overwrite_policy`·`test_existing_receipt_is_append_only`가 GREEN이다. 다만 "확정 영수증 생성에 사용하지 않는다"는 운영 지침은 독립 재검토 전까지 유지하는 것이 타당하다 |
| 56행 | "기존 bootstrap은 기본 의존성 동기화·자동 설치 안내·실패 종료 문제가 남아 있다. 수정·회귀 시험과 독립 검토가 끝날 때까지 실행하지 않는다" | **절반만 낡음.** `bootstrap.sh`는 수정·회귀 시험이 끝났다(라벨 필수, `AUTO_INSTALL=1` 없으면 `uv sync` 안내만). `bootstrap.ps1`은 정적 검토만 했고 독립 검토는 두 파일 모두 미완료다. "수정·회귀 시험"과 "독립 검토"를 분리해 서술해야 한다 |
| 72행 | "PowerShell native 명령 실패는 `$LASTEXITCODE`를 검사하는 수정본과 회귀 시험 전 성공으로 처리하지 않는다" | **유효.** pwsh 부재로 실행 검증이 없다. 그대로 둔다 |
| 33·35행 | 저장소 위생·링크/마운트의 사람 확인 요구 | **유효.** OS 수준 검사는 자동화되지 않았다 |

세 구분을 섞지 않는다. (a) 회귀 시험 완료(POSIX 경로), (b) Windows 실행 미검증, (c) 독립 재검토 미완료. (a)가 (b)나 (c)를 대신하지 않는다.

## 4. 잔여 게이트

- `approval_status`는 `cannot_proceed`를 유지한다. Pi 개발 명세 번들의 spec 관점이 실패한 사실은 나머지 14개 리뷰어 판정으로 대체되지 않으며, PM의 사람 결정으로도 PASS로 바꾸지 않는다. 해당 번들은 관점 실패를 해소한 재실행이 필요하다.
- `execution_authorized`는 `false`를 유지한다. 이 기록은 실행 승인을 생성하지 않는다.
- §0 manifest에 대한 다른 제공자(작성자 anthropic과 다른 제공자)의 필수 관점 독립 재검토가 미완료다. R-01은 그 재검토 PASS 전에 Done으로 전환하지 않는다.
- 보호 경로(`.pi/`, `.specify/`, `docs/evidence/`)와 vendored 도구(`.agents/`, `_bmad/`)는 이 작업에서 stage하지 않는다.

## 5. 다음 행동

1. PM이 §1.1의 여섯 단위 선행 결정을 기록한다. 특히 M4-01 안 A/안 B를 확정한다.
2. 결정 후 별도 변경 단위에서 백로그·그래프를 정정하고 §3의 정정 문구를 적용한다. R-01은 결정 기록까지이며 문서 수정은 포함하지 않는다.
3. §0 manifest로 독립 재검토를 요청한다.
4. Windows 실행 검증은 R-04 (#44), 거버넌스 소유권은 R-03 (#43), 조사 하네스는 R-06 (#46)에서 처리한다.

## 변경 이력

| 버전 | 날짜 | 내용 |
|---|---|---|
| 1.0 | 2026-09-06 | 최초 작성. 선행관계 6건 대조, 105건 재분류, F-13·학교 PC 안내 대조 |
