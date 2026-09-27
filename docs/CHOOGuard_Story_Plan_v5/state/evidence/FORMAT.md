# 영수증 형식

`state/evidence/*.json` 이 따르는 형식이다. 지금까지 매번 즉흥적으로 썼고, 그래서 **어떤 영수증에는
있고 어떤 영수증에는 없는 필드**가 생겼다. 형식을 고정한다.

이 문서는 2026-09-28 에 `design/fps-ai-20260925/VALIDATION.md` 를 읽고 만들었다. 그 문서가
가진 두 가지가 내 영수증에 없었다 — **재발 신호**와 **자가평가**다.

---

## 1. 영수증이 하는 일

**무엇을 증명했고 무엇을 증명하지 않았는지를 구분해 남긴다.** 통과 개수를 자랑하는 문서가 아니다.

`VALIDATION.md` 의 기준을 그대로 쓴다 — "설계 산출물 검증과 미구현 게임의 수용 시험을 구별한다."
번역하면: **시험이 통과한 것과 제품이 되는 것은 다르다.**

## 2. 필수 필드

| 필드 | 뜻 |
|---|---|
| `storyId` | v5 Story. 지금은 전부 `CS-EXEC.01.01` |
| `updatedUtc` | ISO 8601 UTC |
| `status` | `SCOPED_INCREMENT_VERIFIED` / `PARTIAL` / `BLOCKED` |
| `acceptanceStatus` | `NOT_ACCEPTED` 가 기본. 제품 수용은 별도 판단이다 |
| `scope` | **무엇에 한정한 영수증인지.** 범위를 적지 않으면 전체를 증명한 것처럼 읽힌다 |
| `rubric`·`rubricVersion` | 판정 기준 |
| `designRef` | 이 작업의 설계 정본 |
| `run` | 브랜치·실행 방식·`totals`. 건너뛴 것이 있으면 `skippedNote` |
| `checkpoints[]` | 아래 §3 |

## 3. `checkpoints[]`

```json
{ "name": "...", "result": "...", "evidenceRef": "...", "summary": "..." }
```

`result` 에 쓰는 값과 그 뜻:

| 값 | 뜻 |
|---|---|
| `PASS` | **관측으로 확인했다.** 단언이 통과한 것만으로는 부족하다 |
| `FAIL` | 확인했고 못 미친다 |
| `PARTIAL` | 일부만 성립. 무엇이 빠졌는지 `summary` 에 |
| `NOT_VERIFIED` | 확인할 수 없었다. 사람이 해야 하거나 대상이 없다 |
| `NOT_IMPLEMENTED` | 만들지 않았다 |
| `NOT_CLAIMED` | **주장하지 않는다.** 법정 적합처럼 내가 판단할 자격이 없는 것 |
| `KNOWN_GAP` | 알고 남긴 구멍 |
| `FAIL_AT_THE_TIME` | 과거 영수증을 정정할 때. 그때 `PASS` 로 적었지만 사실이 아니었다 |

**`PASS` 의 기준이 가장 중요하다.** 2026-09-24 에 점검표를 `PASS` 로 적었는데
`AttachedTag != null` 과 `Renderer.enabled` 만 확인한 것이었다. 다음 날 화면을 띄우니
아무것도 그려지지 않았다. **그 단언은 두 경우 모두 통과한다.**

## 4. 정정 필드

과거 영수증이 틀렸으면 **지우지 않고** 새 영수증에서 뒤집는다. 지우면 왜 틀렸는지가 사라진다.

```json
"supersedes": {
  "receipt": "...", "checkpoint": "...",
  "wasRecordedAs": "PASS", "correctedTo": "FAIL_AT_THE_TIME",
  "why": "무엇을 근거로 PASS 라 했고 그 근거가 왜 부족했는지"
}
```

같은 영수증 안에서 자기 판단을 고칠 때는 `correction` 을 쓴다
(`what` / `wrong` / `correct` / `consequence`).

## 5. `recurrenceSignal` — 새로 추가

`VALIDATION.md` 가 교훈에 **재발 신호**를 함께 적었다.

> 재발 신호는 '동적/자율' 요구의 수용 기준이 대사·seed·고정 pack 수만 확인하는 경우다.

교훈만 적으면 다음에 알아보지 못한다. **"이 문장이 보이면 같은 실수를 하고 있는 것"** 을 적는다.

```json
"recurrenceSignal": [
  "증거가 '컴포넌트가 존재한다' 로만 이루어져 있다 — 화면·월드 상태를 본 것이 아니다",
  "'시험이 N개 통과했다' 가 PASS 의 유일한 근거다",
  "기존 코드를 '없다' 고 판단한 근거가 develop 한 브랜치뿐이다",
  "끝점·좌표를 보정해 놓고 보정량을 재지 않았다",
  "수치가 맞아 보여서 눈으로 확인하지 않았다"
]
```

지금까지 이 신호들이 **각각 실제로 한 번씩 나타났고 그때마다 틀렸다.**

## 6. `selfAssessment` — 새로 추가

`VALIDATION.md` 의 5축을 쓴다. 점수는 산출물에 대한 것이지 제품 완성도가 아니다.

```json
"selfAssessment": {
  "accuracy": { "score": 4, "note": "..." },
  "completeness": { "score": 3, "note": "..." },
  "clarity": { "score": 4, "note": "..." },
  "actionability": { "score": 4, "note": "..." },
  "concision": { "score": 4, "note": "..." },
  "overall": 3.8,
  "topGaps": ["...", "..."]
}
```

2점 이하가 있으면 중대 품질 문제로 보고 그대로 적는다. **평균을 올리려고 축을 고르지 않는다.**

## 7. 선택 필드

| 필드 | 쓸 때 |
|---|---|
| `followsFrom` | 앞선 영수증을 이어받을 때 |
| `dataRef` | 산출 데이터(좌표 JSON 등) |
| `method` | 재현 방법. 하네스·건너뛴 것 |
| `decision` | 설계 판단과 이유 |
| `limits` | 결과를 좌우하는 설정값 |
| `myErrorsWhileBuildingTheTool` | 도구를 만들며 틀린 것. **제품 결함과 섞지 않는다** |
| `harnessBugsNotProductBugs` | 같은 이유 |

## 8. 쓰지 말 것

- **통과 개수만으로 `PASS`.** 무엇을 관측했는지 적는다.
- **범위 없는 영수증.** `scope` 가 없으면 전체를 증명한 것으로 읽힌다.
- **틀린 판정을 조용히 수정.** `supersedes` 나 `correction` 으로 남긴다.
- **내가 판단할 자격이 없는 것을 `PASS`.** 법정 적합·안전 인증은 `NOT_CLAIMED` 다.
- **도구 버그를 제품 결함으로 집계.** 실제 결함 수가 부풀려진다.
