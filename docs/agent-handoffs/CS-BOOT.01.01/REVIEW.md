# CS-BOOT.01.01 검수 인계

<!-- AUTO-GENERATED: REVIEW.json 및 final-verification-repaired.json 기반 reader view -->

**IMPLEMENTED_NOT_VERIFIED / NOT_ACCEPTED** · claim **RELEASED** · `records=[]`

## 원본과 검수 범위

- 검수 인계 원본: [REVIEW.json](REVIEW.json)
- 최종 실행 증거: [final-verification-repaired.json](../../build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/final-verification-repaired.json)
- 현재 sourceTreeDigest: `ebc9e92b0ee88779e10c6ea2c195cc1a3d22d7db8bca7198fc5c296f0cca23e0`
- Terra/Sol 검수 대상: **이전** digest `763c09ae5ab28f61a978f58fa5fe548184ee460622e1523a14e8f88ed83abbe3`. 수정후 Terra/Sol 재검수는 **NOT_RUN**이다.

## 지적과 처리

| 구분 | 지적 | 처리 |
|---|---|---|
| Terra MEDIUM1 | 실패 BuildReport의 부분출력 inventory 누락 | 결과 판정 전 reported files 수집, 부분 hash 보존, UNOBSERVED/INCOMPLETE 이유 및 실패 전파 |
| Sol MEDIUM1 | PLAN 등의 현재값과 과거 단계 혼재 | 현재 source/evidence/산출물/claim/task 정규화, 과거값 보존 |
| Sol MEDIUM2 | GENERATED_FILES 현재 disposition이 복사 대기로 남음 | 현재 RECONCILED_IN_ROOT, 과거 disposition은 stageProvenance |
| Sol MEDIUM3 | 제품78 동일성이 전체79 수집처럼 읽힘 | 새 보고서에서 collectionScope와 historicalEvidenceNotes로 구분 |

Sol의 개별 3건 분리는 조정자가 전달한 후속 보고 기준이다. 총 개별 지적은 **4건**, 승인 수리 범주는 **2개**다. 이전 Terra WARNING / Sol APPROVE-with-handoff-reconciliation은 현재 소스 재승인이나 story 수용이 아니다.

## 검증 결과와 한계

회귀 RED는 Unity exit2, 46 total / 44 pass / 2 fail이며 Failed·Cancelled 각각 expected1/actual0이었다. 수리 후 **EditMode 51/51**, **PlayMode 1/1**, **strict baseline 12/12 PASS**다. 최종 네 Unity 호출은 exit0/timeout=false지만 Windows CLI/baseline은 **build/run NOT_RUN, outputs[]**다.

실패 회귀는 private seam을 통한 **모의 결과와 실제 scratch 파일** 검사로, 실제 failed Unity BuildReport 통합 실행은 아니다. 새 Mac full build도 수행하지 않았다.

command.sourceHashes는 **제품78개**, manifest/receipt digest는 schema 포함 **79개**다. 새 PlayMode/Windows/baseline만 schemaSha256를 별도 사전 수집했다. EditMode 및 과거 명령을 79개 실행 직전 수집 증거로 해석하지 않으며 원시 자료를 사후 변경하지 않았다.

Terra는 이전 소스의 GUID/localID/persistence/경로/membership identity oracle을 같은 소유 계약의 타당한 직접 검사로 판단했다. hide-flags 인과 toggle 실험은 없으며 상세 oracle 대체안 편집 전 승인도 주장하지 않는다. 과거 Mac 전후 3파일 raw 사본 부재에 따른 전체 field-level diff UNKNOWN, 현재 소스와 과거 Mac output 동일성 미입증, 최초 두 generator 비교 UNKNOWN을 유지한다.

Windows/Player/수동 화면·포인터·IME 미실행, standalone OFL NOT_CONFIRMED, coverage NOT_MEASURED, graph STALE/semantic UNKNOWN, 임의 nonempty component cross-reference 일반성 미검증 때문에 **NOT_ACCEPTED**다.

## Root 직접 대조 — 조정자 전달 사실

조정자는 Root가 최종79 manifest/digest, 변경 소스2개와 diff, 네 호출 product78/exit0/timeout=false, 새3호출 schema, EditMode51·PlayMode1의 XML 이름·시각·로그 hash, Windows NOT_RUN, 현재 disposition 및 RELEASED/records[]를 확인했다고 전달했다. 기존 strict helper의 최종 digest 12PASS 재실행도 전달받았다. 여기서는 해당 파일을 직접 인용하지 않으며, 이 전달을 새 Terra/Sol 독립 검수라고 표현하지 않는다.

[handoff-verification-final.json](../../build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/handoff-verification-final.json)은 **이번 두 MD 생성 전** 검증이다. 이 파일과 대응 JSON, 원시 command/XML/receipt를 수정하지 않았다. 이번 보완은 읽기용 MD 두 개 생성만이며 새 시험·전수검증·수용 판정이 아니다.

<!-- /AUTO-GENERATED -->
