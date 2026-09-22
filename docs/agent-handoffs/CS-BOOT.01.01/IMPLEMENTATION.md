# CS-BOOT.01.01 구현 인계

<!-- AUTO-GENERATED: IMPLEMENTATION.json 및 final-verification-repaired.json 기반 reader view -->

**IMPLEMENTED_NOT_VERIFIED / NOT_ACCEPTED** · claim **RELEASED** · `records=[]`

## 기준 자료

- 구현 원본: [IMPLEMENTATION.json](IMPLEMENTATION.json)
- 최종 실행 증거: [final-verification-repaired.json](../../build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/final-verification-repaired.json)
- sourceTreeDigest: `ebc9e92b0ee88779e10c6ea2c195cc1a3d22d7db8bca7198fc5c296f0cca23e0`
- 이 문서는 원본 JSON의 읽기용 요약이며 새로운 실행·수용 기록이 아니다.

## 구현 및 수리

Bootstrap scene의 Camera/overlay Canvas/EventSystem 각 1개, 정적 TMP 표식, Input System action 참조, 소유 URP 자산, 생성기·validator·baseline과 실제 EditMode/PlayMode 시험을 구현했다.

검수 후 제품 소스 변경은 다음 두 파일이다.

- `Assets/ChooGuard/Editor/Bootstrap/BuildBaseline.cs`: 반환된 report의 기존 reported-file 경로/hash를 결과 판정 전에 수집한다. 실패 시 부분 hash를 보존하고 FAILED 예외를 전파한다. report 미반환은 UNOBSERVED, 열거/hash 실패는 INCOMPLETE를 이유에 기록한다. 빈 outputs는 파일 부재 증거가 아니다.
- `Assets/ChooGuard/Tests/EditMode/Stories/CSBOOT0101Tests.cs`: Failed/Cancelled 회귀와 null/예외/성공 관측 시험을 추가했다.

preflight → module/Windows host → validator → BuildPlayer 순서, public API/schema는 유지했다. crawler나 부분 산출물 삭제는 없다. handoff의 현재값과 과거 stage 값은 분리했다. 정확한 소스 before/after 및 diff는 최종 JSON의 `sourceChanges`를 따른다.

## 실제 검증 결과

| 구분 | 결과 |
|---|---|
| 회귀 RED | Unity exit2, 46 total / 44 pass / 2 fail; Failed·Cancelled 각각 expected1 / actual0 |
| 최종 EditMode | Unity exit0, 51/51 PASS |
| 최종 PlayMode | Unity exit0, 1/1 PASS |
| Windows CLI / baseline | 각각 Unity exit0, build/run NOT_RUN, outputs[] |
| strict baseline | 12/12 PASS |

최종 Unity 호출은 timeout=false다. 실패 회귀는 **private seam의 모의 BuildResult/열거 결과와 실제 scratch 파일**을 사용했다. 실제 failed/cancelled Unity BuildReport 통합 실행으로 주장하지 않는다. 이 MD 보완에서는 source/test/Unity를 실행하거나 변경하지 않았다.

## 입력 동일성과 증거 한계

command.sourceHashes는 Assets/Packages/ProjectSettings **제품78개**다. manifest/receipt digest는 baseline.schema.json을 포함한 **79개**다. 새 PlayMode/Windows/baseline은 schemaSha256를 별도로 사전 수집했지만 EditMode 명령에는 없으며, schema의 before/final 일치를 실행 직전 79개 동시 수집으로 확대하지 않는다. 기존 원시 명령은 수정하지 않았다.

Windows 및 Mac Player 실행 NOT_RUN, 수동 화면·포인터·IME MANUAL_NOT_RUN, standalone OFL NOT_CONFIRMED, coverage NOT_MEASURED, graph code STALE / semantic UNKNOWN을 유지한다. 과거 Mac 빌드는 이전 입력의 증거이며 전후 3파일 직전 raw 사본 부재로 전체 field-level diff UNKNOWN이다. 첫 두 generator 비교 및 임의 nonempty VolumeComponent cross-reference 복제 일반성도 미검증이다.

## Root 전달 사실과 종료

조정자는 Root가 최종79 digest, source변경2개/diff, 네 호출 product78 및 exit/timeout, 새3호출 schema, XML/로그, Windows NOT_RUN, disposition과 claim을 직접 대조하고 strict helper 12PASS를 재확인했다고 전달했다. 이는 **조정자 전달 사실**이며 새 Terra/Sol 독립 검수가 아니다.

[handoff-verification-final.json](../../build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/handoff-verification-final.json)은 **IMPLEMENTATION.md와 REVIEW.md 생성 전** 인계 검증이다. 두 새 MD를 검증했다고 주장하지 않으며 원본은 수정하지 않았다.

실제 provider model/effort 독립 확인은 UNKNOWN; Astra/low는 발진 설정 증거다. claim RELEASED, records=[], 제품 freeze를 유지하며 후속 실행 없이 종료한다.

<!-- /AUTO-GENERATED -->
