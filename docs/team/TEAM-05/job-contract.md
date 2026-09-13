# TEAM-05 · 재시작 가능한 제작 job·저장 계약 (isolated_proposal 초안)

## 0. 이 문서의 지위

- mode: `isolated_proposal`
- canonicalWriteAllowed: `false`
- workId: `TEAM-05` · issue: `73` · phase: `candidate`
- 이 문서는 후보 단계의 **초안**이다. 스스로를 accepted / approved / final / complete 로
  표기하지 않는다. 어떤 PASS 도 런타임·설치·네트워크·Unity·게시·병합 권한을 만들지 않는다.
- 범위: 합성 파일 위의 job 계약(입력/출력 경계, 원본·캐시·임시·후보 분리, 중단·재개,
  중복·불일치 거부, 보존 경계)만 다룬다. 제품·안전 보증이 아니다.

지정 출력:
- `docs/team/TEAM-05/job-contract.md` (이 문서)
- `scripts/team/TEAM-05/job_fixture.py`
- `scripts/team/TEAM-05/test_job_fixture.py`

## 1. 필수 완비 필드 — jobID · 원본/캐시/임시/후보 · 재개계약

### 1.1 jobID

`jobID = sha256(json({names: sorted(input set), output: output_rel}))[:24]`

- jobID 는 **입력 집합과 출력 대상**으로 결정된다. 입력 **바이트는 jobID 에 넣지 않는다.**
  그래야 원본이 바뀌어도 같은 job 으로 남고, 재개 시 `INPUT_HASH_MISMATCH` 로 검출된다
  (identity 가 바뀌어 조용히 다른 job 이 되는 것을 막는다).
- 예산(budget)은 jobID 에 넣지 않고 checkpoint 에 저장한다. 재개 시 예산이 다르면
  `CONTRACT_MISMATCH` 로 거부한다.
- `output_rel` 은 후보 저장소 안의 상대 경로이며, jobID 는 그 대상에 대해 안정적이다.

### 1.2 저장소 분리 (원본 / 캐시 / 임시 / 후보)

네 저장소는 job 루트 아래 별도 디렉터리이며 접근자도 분리돼 서로 바뀌어 쓸 수 없다.

| 저장소 | 디렉터리 | 내용 | 쓰기 규칙 |
|---|---|---|---|
| 원본 | `original/` | 합성 입력 바이트 (불변 취급) | job 이 연 뒤 digest 로 결속; 바뀌면 재개 거부 |
| 캐시 | `cache/<입력파일 sha256>.part` | 입력에서 파생한 결정적 확장 | 내용 주소 지정; 바이트가 일치할 때만 재사용, 불일치 시 덮어쓰지 않고 거부 |
| 임시(작업) | `work/<jobID>/` | `checkpoint.json`, `lock.json`, `observation.json` | checkpoint 는 매 단위마다 원자적 교체; lease 는 실행 중에만 존재 |
| 후보 | `candidate/<output_rel>` | 공개 후보 출력 | write-new 전용, 기존 출력 절대 덮어쓰지 않음 |

- 후보 출력은 **별도 저장소에 새 파일로만** 생성된다(`open("xb")`). 원본·캐시·임시 어디에도
  후보 바이트를 쓰지 않는다. 경로 이탈(`..`, 절대경로, `\`, `:`, `.git`)은 `PATH_ESCAPE` 로 거부한다.
  저장소 안의 파일을 가리키지 않는 빈 이름·`.`·`./` 도 같은 코드로 거부한다(저장소 내부를
  가리키지 않으므로 조용히 통과시키지 않는다).

### 1.3 재개계약

checkpoint 필드(요지): `schemaVersion, jobID, inputDigest, files{name:{sha256,bytes}}, names,
outputRel, budgetBytes, totalUnits, completedUnits, producedBytes, state, holder,
freeBytesBefore`.

- 작업 단위 = 입력 한 줄 → 출력 한 조각(결정적). 단위마다 checkpoint 를 원자적으로 저장한다.
- **중단**: `run(limit_units=k)` 로 k 단위 뒤 멈추면 `state="interrupted"`, lease 해제,
  checkpoint 의 `completedUnits` 는 유지된다.
- **재개**: 같은 입력 집합·출력·예산으로 `begin` 하면 같은 jobID 의 checkpoint 를 찾아
  `completedUnits` 에서 이어간다. 진행 수는 단조 증가하며 0 으로 되돌지 않는다.
- **중복 실행 거부**: lease(`lock.json`)가 있으면 `begin` 은 `JOB_ACTIVE`. 이미 끝난 job 은
  `JOB_DONE`.
- **입력 hash 변경 거부**: 재개 시 `inputDigest` 를 다시 계산해 저장값과 비교, 다르면
  `INPUT_HASH_MISMATCH` — 출력을 만들지 않는다.
- **기존 출력 덮어쓰기 거부**: 대상 후보 파일이 있으면 `OUTPUT_EXISTS` — 기존 바이트를 보존한다.
- **게시 실패 시 lease 해제**: 조립·게시 단계에서 거부(`CACHE_HASH_MISMATCH`,
  `OUTPUT_LENGTH_MISMATCH` 등)가 나면 checkpoint 를 `interrupted` 로 기록하고 lease 를 즉시
  해제한 뒤 오류를 올린다. 그래야 실제로 실행 중이 아닌 job 이 이후 `begin` 에서 `JOB_ACTIVE`
  로 잘못 보고되지 않고, 같은 입력으로 다시 재개할 수 있다.

## 2. 오류 코드 (인계용 — #75·#85)

| code | 뜻 | 발생 지점 |
|---|---|---|
| `PATH_ESCAPE` | 저장소 밖 경로 | 입력/출력 경로 검증 |
| `INPUT_MISSING` | 원본 없음 | manifest |
| `BUDGET_UNDECLARED` | 양의 정수 예산 없음 | begin |
| `JOB_ACTIVE` | lease 보유 중 동일 job 동시 실행 | begin |
| `JOB_DONE` | 이미 끝난 job 재개 | begin / run |
| `JOB_UNKNOWN` | checkpoint 없음 | run |
| `CONTRACT_MISMATCH` | 재개가 예산을 바꿈 | begin |
| `INPUT_HASH_MISMATCH` | 연 뒤 원본이 바뀜 | run |
| `OUTPUT_EXISTS` | 후보 출력이 이미 존재 | begin / run |
| `BUDGET_EXCEEDED` | 선언 예산보다 큼 | run |
| `CACHE_HASH_MISMATCH` | 캐시 바이트 불일치 | 게시 조립 |
| `OUTPUT_LENGTH_MISMATCH` | 조립 길이 ≠ 진행 바이트 | 게시 |

## 3. 보존 경계와 pilot 관측

- 게시 시 `observation.json`(write-new)에 `outputBytes, outputDigest, freeBytesBefore,
  freeBytesAfter, survey{stores{original,cache,work,candidate}, freeBytes}, retention` 을 기록한다.
- `survey` 는 저장소별 바이트 합과 잔여 여유 공간을 보고한다. **자동 삭제는 없다** — 원본·캐시·
  임시 어느 것도 회수 목적으로 지우지 않는다. lease 파일만 해제 시 제거된다.
- nonGoals: 전체 환경 정리/자동 삭제, 특정 기기·위치 할당, CV 모델 캐시 재개 — 주장하지 않는다.

## 4. 출처 · 다이제스트 · 드리프트 (H1)

- 동결 기준·브리프가 명시한 기준: `sourceRef ac7f84edb56477b2d7039e20fec91e7e51b0d3e6`.
- 이 산출물 트리의 `docs/context/work-graph.json` 은 `snapshot.sourceRef =
  e7a6bb7a7fff0c301c04f93aeea8850dbde34779` (`capturedAt 2026-09-12T09:39:41Z`) 를 **선언**한다.
  두 값은 **다르다 — 드리프트를 명시한다.**
- 독립 구현 제약상 `git` 을 실행하지 않았다. 따라서 이 트리의 HEAD 커밋이 `ac7f84e` 와
  동일한지 여부는 **직접 측정하지 않았다(미검증).** 이는 "ref 에서 볼 수 없다"는 서술이 아니라
  "제약으로 실행하지 않았다"는 기록이며, 동결 기준이 제시한 `git show <ref>:path` 대조 경로는
  실행하지 않았다. 위상 값은 이 트리의 동일 경로 파일에서 읽었다.
- 읽은 문맥(이 트리, 파일 읽기만):
  - `docs/context/work-orders/073.json` — 작업 브리프(워크오더) 사본.
  - `docs/context/work-graph.json` — `items[number=73]` 계약.
- 인용 참조 파일과 sha256(이 트리에서 계산):

| 경로 | sha256 | declaredAvailability |
|---|---|---|
| `scripts/dev/native_manifest.py` | `e31f05957e204fd3207af58e866cec5f2f027219857f0240805fa2ccdaf64f83` | local_unpublished |
| `scripts/dev/probe_journal.py` | `27304b509d0190edf02ef9fb22fe7ff650d5651adc2ad19ec5260ae5ce48d49f` | local_unpublished |
| `scripts/art/validate_reference_assets.py` | `10784b91d270bb708882d8b46e936f8d7c4d256aad807bf72e21b53b2e320df5` | published |

## 5. 그래프 위상 (G6)

이 트리 `items[number=73]` 에서 추출(표기 `(consumerPhase→producerPhase)`):

- 선행: `120(candidate→accept)` — 전부.
- 후속: `58(accept→candidate)`, `75(candidate→candidate)`, `75(accept→accept)`, `85(accept→candidate)`.

동결 기준 §1 G6 의 열거와 일치한다. 후보(candidate) 단계 gate 는 `consumerPhase == candidate`
인 선행, 즉 #120 에만 구속된다. 후속 #58·#75·#85 는 이 후보 산출물을 소비하는 쪽이며 후보
단계의 차단 요인이 아니다. (#75 는 candidate·accept 양 phase 에서 이 계약을 소비한다.)

## 6. 중단 조건 처리 (H5)

- **SC-1 (필수 입력의 ref/hash 미비)**: #120 accept manifest 는 이 후보 단계에 제공되지 않았다.
  `scripts/dev/native_manifest.py`·`scripts/dev/probe_journal.py` 는 `local_unpublished` 이며
  접근 가능한 sourceRef/hash 결속이 없다. 따라서 그 바이트를 **쓰는 수용은 정지**하고 결함으로
  기록한다(§4 표, 상태 `not_qualified`). 이 산출물은 두 파일을 호출·복사·의존하지 않았고,
  읽기 전용 참고로만 보았다. job_fixture 는 표준 라이브러리만 쓴다.
- **SC-2 (출력 lock holder)**: 이 트리에서 `candidate:TEAM-05` 외부 holder 는 관측되지 않았다.
  전역 lock registry 조회 수단은 없어 **미검증**으로 남긴다. `job_fixture` 의 lease 는 job 단위
  내부 잠금이며 외부 lock 대체가 아니다.

충돌·미해결: §4 의 ref 드리프트와 SC-1 은 은폐하지 않고 `unknown`/`blocked` 로 등록한다.

## 7. 수용 기준 대응

| # | 수용 기준 | 대응 |
|---|---|---|
| 1 | 원본/캐시/작업/공개후보를 분리 | 4개 저장소 + 분리 접근자; 후보는 `candidate/` 에 write-new (§1.2) |
| 2 | 중단 후 같은 입력으로 재개, 중복 실행 거부 | checkpoint 단위 저장, `completedUnits` 이어가기, lease 로 `JOB_ACTIVE` (§1.3) |
| 3 | 경로이탈/hash불일치/기존출력 덮어쓰기 거부 | `PATH_ESCAPE` · `INPUT_HASH_MISMATCH` · `OUTPUT_EXISTS` (§2) |
| 4 | pilot 관측 크기/잔여 공간 기록, 자동삭제 없음 | `observation.json`/`survey`, 삭제 없음 (§3) |

## 8. 인계 (#75 · #85)

- **input 경계**: 원본 저장소의 명명된 파일 집합 + 그 `inputDigest`(파일별 sha256 + 집계).
- **output 경계**: 후보 저장소의 `output_rel` 단일 파일 + `outputSha256`, `outputBytes`.
- **재개/오류 코드**: §2 표를 그대로 소비하면 된다. 재개는 `begin`+`run`, 중단 관측은
  `state=="interrupted"` 와 `completedUnits`.

## 9. 검증 영수증

```json
{
  "verification": {
    "issue": 73,
    "workId": "TEAM-05",
    "phase": "candidate",
    "mode": "isolated_proposal",
    "canonicalWriteAllowed": false,
    "checks": {
      "C01": {
        "checkId": "C01",
        "declaredCommand": "python3 -m unittest discover -s scripts/team/TEAM-05 -p 'test_*.py'",
        "expectation": "jobfixture와시험을먼저구현;정상/거부모두기대exit",
        "negativeCase": "동일job동시쓰기·inputhash변경·기존output덮어쓰기·미정예산",
        "result": "PASS",
        "artifactPath": "docs/team/TEAM-05/job-contract.md",
        "artifactDigest": {
          "scheme": "sha256",
          "scope": "self",
          "value": null,
          "notEmbeddableReason": "파일은 자기 자신의 다이제스트를 담을 수 없다"
        },
        "provenance": "기준 sourceRef ac7f84edb56477b2d7039e20fec91e7e51b0d3e6(동결 기준·브리프). 위상/계약 값은 이 트리 docs/context/work-graph.json items[number=73] 에서 읽음. 이 트리 snapshot.sourceRef 는 e7a6bb7a7fff0c301c04f93aeea8850dbde34779 로 기준 ref 와 다름(§4 드리프트). git 미실행.",
        "thresholdEvaluation": "통과 임계: discover 가 1개 이상 시험을 수집하고 종료코드 0, 그리고 C01 부정 사례 4종이 각각 지정 코드로 거부 관측되어야 PASS. 관측: 14개 시험 수집, 종료코드 0, JOB_ACTIVE·INPUT_HASH_MISMATCH·OUTPUT_EXISTS·BUDGET_UNDECLARED 네 코드 모두 거부 확인 → PASS. demo 의 freeBytesBefore/After/survey.freeBytes 는 실행 시점의 여유공간이라 매 실행 달라진다 — 이 수치는 재현 대상이 아니다. 결정적 필드(inputDigest·inputSha256·jobID·outputBytes·outputSha256·totalUnits·interrupted/resumedCompletedUnits)는 값이 고정이다.",
        "notRunReason": null,
        "evidenceBlocks": [
          {
            "step": "c01-test-suite",
            "command": "python3 -m unittest discover -s scripts/team/TEAM-05 -p 'test_*.py'",
            "observedOutput": "..............\n----------------------------------------------------------------------\nRan 14 tests in 0.057s\n\nOK\nexit=0"
          },
          {
            "step": "artifact-hashes",
            "command": "python3 -c \"import hashlib; [print(hashlib.sha256(open(f,'rb').read()).hexdigest(), f) for f in ['scripts/team/TEAM-05/job_fixture.py','scripts/team/TEAM-05/test_job_fixture.py']]\"",
            "observedOutput": "5af06ec4db0bd2c6a685b9fe42fef08c62c9462bec4bb42730b5c1b25cdbf156 scripts/team/TEAM-05/job_fixture.py\n1e0910a5529fc4c5180723edc21ce9070f89f4dd69d944d515905196dfcfc4f7 scripts/team/TEAM-05/test_job_fixture.py"
          },
          {
            "step": "input-output-hash",
            "command": "TMPD=$(mktemp -d) && python3 scripts/team/TEAM-05/job_fixture.py demo --root \"$TMPD\"; rm -rf \"$TMPD\"  # 프로젝트 밖 새 빈 디렉터리, 기존 상태 없음",
            "observedOutput": "{\"finalState\": \"done\", \"inputDigest\": \"859621202cd70591129e0e26bcd7a9ec48a1601a3f9a59f15eaad10bca221a17\", \"inputSha256\": \"3eca7ea48b0da0ad30bee679c92c7b68d487547068b6914d10a64e8cedb03f51\", \"interruptedCompletedUnits\": 1, \"interruptedState\": \"interrupted\", \"jobID\": \"d0312de787494e02a3a5547c\", \"observation\": {\"freeBytesAfter\": 21091221504, \"freeBytesBefore\": 21091254272, \"jobID\": \"d0312de787494e02a3a5547c\", \"outputBytes\": 80, \"outputDigest\": \"4db773e1f8e9c37c043a5a641907a86e06639ee0ddd35485de7d30d67ed741b6\", \"retention\": \"originals, cache and work are retained; nothing is deleted automatically\", \"survey\": {\"freeBytes\": 21091221504, \"stores\": {\"cache\": 80, \"candidate\": 80, \"original\": 20, \"work\": 597}}}, \"outputBytes\": 80, \"outputSha256\": \"4db773e1f8e9c37c043a5a641907a86e06639ee0ddd35485de7d30d67ed741b6\", \"resumedCompletedUnits\": 1, \"totalUnits\": 3}\nexit=0"
          }
        ]
      }
    }
  }
}
```

## 10. 미검증 / 열린 항목

- 이 트리 HEAD 커밋이 기준 ref `ac7f84e` 와 동일한지 — 미검증(git 미실행).
- `candidate:TEAM-05` 전역 lock registry 상태 — 미검증(조회 수단 없음).
- #120 accept manifest 부재로 `native_manifest.py`·`probe_journal.py` 의 qualified ref/hash —
  `not_qualified`(§6 SC-1).

### 이번 후보에서 보강한 거부 경계

초기 구현을 직접 실행해 확인한 두 결손을 고치고 시험으로 덮었다(현재 14 시험 전부 통과).

- `portable_relative` 가 `.`/`./` 에서 `IndexError` 를 올려 typed 거부를 어겼다 → 이제
  `PATH_ESCAPE` 로 거부한다.
- 게시(`_publish`) 실패 시 lease 가 남아 실행 중이 아닌 job 이 `JOB_ACTIVE` 로 오보고되고
  재개가 막혔다 → 이제 checkpoint 를 `interrupted` 로 기록하고 lease 를 해제한 뒤 오류를
  올린다. 시험 `test_failed_publish_releases_lease_and_stays_resumable` 이 고정한다.
