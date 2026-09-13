# 증거 번들 — 이슈 #70 후보 B (13구역·번들·portal 경계)

이 디렉터리는 **후보 산출물이 아니라 실행 증거**다. 후보 4경로는
`foundation/world/zone-boundaries.json`, `scripts/art/validate_zone_boundaries.py`,
`scripts/art/test_zone_boundaries.py`, `docs/art/zone-boundaries.md` 이며, 여기 있는 것은
그 4경로를 실제로 실행해 받은 원문 출력이다.

- base commit: `c1a7b78d895bd87216b64a6e5ca5d26eb6d85b3e`
- 모드: `isolated_proposal`, `canonicalWriteAllowed: false`
- **여기의 종료코드와 출력은 증거이지 정책 PASS 나 수용 결정이 아니다.**

## 실행법

```sh
sh docs/evidence/world/2026-09-13-zone-boundaries/run.sh
```

`run.sh`는 이 디렉터리 안에만 쓴다. 실행 전후로 후보 4경로와 읽기 전용 상류 계약
`foundation/world/facility-coverage.json`의 sha256을 `hashes-before.txt` /
`hashes-after.txt`에 남기며, 두 파일은 **바이트 단위로 동일**해야 한다(실행이 대상을
바꾸지 않았다는 증명). `git diff --no-index hashes-before.txt hashes-after.txt`로 확인한다.

## 이 디렉터리의 파일

| 파일 | 내용 |
|---|---|
| `run.sh` | 위 실행을 재현하는 스크립트. 받은 출력과 receipt를 여기에만 쓴다 |
| `commands.tsv` | `이름<TAB>실행한 명령 문자열<TAB>종료코드` |
| `<name>.stdout` / `<name>.stderr` / `<name>.exit` | 명령별 실제 출력 원문과 종료코드(손으로 쓰지 않음) |
| `hashes-before.txt` / `hashes-after.txt` | 후보 4경로 + 상류 계약의 sha256. 두 파일은 바이트 동일 |
| `SHA256SUMS.txt` | 후보 4경로의 `shasum -a 256` |
| `receipt.json` | 명령·종료코드·핵심 결과 한 줄·대상 불변 여부. run.sh가 생성 |
| `fixtures/` | 평가 불가 입력 재현용 |

## 무엇을 왜 검증했는가

| 이름 | 검증 대상 | 기대 |
|---|---|---|
| `validate_default` | 후보 문서 4경로 정합성 | `result: pass`, exit 0 |
| `validate_array` | 평가 불가 입력 `[1,2,3]` | `result: not_run`, exit 2 — 규칙 위반으로 뭉개지 않음 |
| `validate_missing` | 없는 파일 | `result: not_run`, exit 2 |
| `validate_truncated` | 잘린 JSON | `result: not_run`, exit 2 |
| `validate_non_utf8` | 비 UTF-8 바이트 | `result: not_run`, exit 2 |
| `tests` | `test_zone_boundaries.py` 25개 시험 | 전부 OK, exit 0 |
| `discover` | `scripts/art` 전체 시험(36개) | 전부 OK, exit 0 |
| `counts` | 13구역·12연결·3번들·12portal·3 cross-bundle·7질문을 **직접 센** 값 | 아래 수치와 일치 |
| `binding` | `zone-boundaries.json`의 `coverage_source.sha256` vs 실제 coverage 파일 해시 | `match=True` |
| `upstream_unchanged` | 상류 계약 파일 해시 | `1e9700ab...3b350` |

핵심 수치(손으로 쓰지 않고 `counts.stdout`에서 읽는다):

```
coverage: zones=13 connections=12 constraints=1
boundaries: bundles=3 zones=13 portals=12 cross_bundle=3 questions=7
portals_with_dimensions=0
```

`portals_with_dimensions=0`은 "근거 없는 portal 개구부 치수를 확정 표기하지 않았다"의
직접 센 값이다. 12개 portal의 `opening_width_m`·`clear_height_m`는 전부 `null`이다.

## 종료코드 계약

| 코드 | 의미 |
|---|---|
| 0 | `pass` — 모든 규칙을 평가했고 위반 없음 |
| 1 | `fail` — 문서는 잘 형성됐으나 규칙 위반 |
| 2 | `not_run` — 입력을 평가할 수 없음(없는 파일, 비 UTF-8, 깨진/잘린 JSON, 최상위가 객체가 아님, 문서 수준 리스트 타입 불일치) |

문서 `docs/art/zone-boundaries.md`의 표와 이 표, 그리고 검사기 `main()`의 반환값은
같은 계약을 쓴다(`scripts/art/test_zone_boundaries.py::test_exit_codes_match_the_documented_contract`
가 실제 프로세스 종료코드로 확인한다).

## 재현 입력 (`fixtures/`)

`run.sh`가 매 실행마다 다시 만든다. 손으로 만든 파일이 아니다.

- `array.json` — `[1,2,3]` (최상위가 객체가 아님)
- `truncated.json` — `{"schema_version":"1.0"` (잘린 JSON)
- `non-utf8.json` — `{"schema_version":"\xff\xfe"}` (비 UTF-8)
- `absent.json` — 의도적으로 만들지 않는다(없는 파일)

## 실행하지 않은 것 (NOT_RUN)

- claim holder 원격 브랜치 `docs/70-zone-coverage-boundaries` 대조 — **익명 비교를 위해 의도적으로 조회하지 않았다.**
- #82 로컬 구현(미푸시)의 구역/portal ID 대조 — 이 워크트리에서 접근 불가.
- V02·V03 영상의 구역 귀속 — 사람 육안 검수가 필요하다.
- 실측 배치·시설 일치·안전 수용 검증 — 이 후보 범위 밖이며 증거가 없다.
- GitHub 이슈/프로젝트 상태 재조회 — 이 후보는 저장소 파일만 다룬다.

`tests`·`discover`의 통과는 **문서 정합성 시험의 통과**일 뿐이며, 정책 PASS·수용·실측·
안전 판단으로 승격하지 않는다.
