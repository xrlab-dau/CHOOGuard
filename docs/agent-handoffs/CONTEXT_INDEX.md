# CHOOGuard 문맥 인덱스

구현 상태: **IMPLEMENTED_NOT_VERIFIED** · 계획 readiness: **PLAN_READY** · revision `6-review-inventory-handoff`

- 정본: `docs/CHOOGuard_Story_Plan_v4/plan.json` (`CHOOGUARD-ROLLING-STORY-PLAN-004`), GLOBAL_CONTRACT 및 basis/v3 기술 기준. 정본 dependency/acceptance/testKinds/status는 유지한다.
- 현재 story: CS-BOOT.01.01 / candidate / fixture. `CS-BOOT.01.01/PLAN.json`, `PLAN.md`, `CONTEXT.json`, `GENERATED_FILES.json`, `IMPLEMENTATION.json`, `REVIEW.json`을 읽는다.
- 현재 sourceTreeDigest: `ebc9e92b0ee88779e10c6ea2c195cc1a3d22d7db8bca7198fc5c296f0cca23e0`.
- 현재 evidence root: `docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4`; 최종 `final-source-manifest.json`, `final-verification-repaired.json`을 사용한다.
- 정본 소유 제품 80경로 + 별도 승인 `.gitignore`가 root에 존재한다. handoff 산출물 상태는 PRODUCED_NOT_ACCEPTED, generated disposition은 RECONCILED_IN_ROOT. 초기 scratch 경로·해시·대기 상태는 명시적 stageProvenance 및 수리 전 사본에 보존한다.
- 활성 장부: `docs/CHOOGuard_Story_Plan_v4/state/progress.json`; EXT-UNITY는 로컬 Editor/기존 라이선스만 SATISFIED. `records=[]`. 기존 claim은 이번 수리 중 ACTIVE였다가 현재 **RELEASED**이며 claims가 빈 목록인 것은 아니다.
- 최신 실제 검증: EditMode **51/51 PASS**, PlayMode **1/1 PASS**, 각각 Unity exit0/timeout=false, 실패·skip·inconclusive 0. strict baseline **12/12 PASS**. 새 Windows CLI와 baseline은 **NOT_RUN** receipt다.
- 회귀 RED: `editmode-inventory-red` 실제 Unity exit2, 46 total / 44 pass / 2 fail. 모의 Failed/Cancelled와 실제 scratch 부분파일에서 expected1 / actual0. 해당 두 사례 및 관측 오류·성공 경로는 최신 suite에서 통과했다. 실제 Unity failed BuildReport 통합 실행으로 주장하지 않는다.
- 명령의 sourceHashes는 제품 **78개**로 schema 제외. manifest/receipt digest는 schema 포함 **79개**. 새 PlayMode/Windows/baseline command는 schemaSha256를 별도 수집했다. EditMode 및 과거 command는 79개 실행 직전 수집 증거가 아니며 원시 파일을 사후 보강하지 않았다. 새 보고서 historicalEvidenceNotes가 이 제한을 기록한다.
- Terra MEDIUM1 + Sol 후속 MEDIUM3의 개별 4지적을 승인 수리 범주2(부분출력 inventory / handoff·수집범위 정합성)에서 처리했다. 독립 검수는 **이전 digest 763c09…** 대상이며 수정후 Terra/Sol 재검수 NOT_RUN. Root의 이전 baseline12 PASS 역시 과거 소스 증거다.
- 과거 Mac 빌드는 다른 pre-build digest의 별도 성공 증거이며 현재 소스/Windows/Player 실행 수용이 아니다. 직전 3파일 raw 사본이 없어 전체 field-level diff UNKNOWN. hidden flags 인과 toggle 실험 없음. 상세 identity oracle 편집 전 승인 주장을 하지 않는다. Terra의 유효한 동일 소유계약 판정은 과거 소스에만 적용된다.
- 과거 baseline bytes는 현재 evidence root의 `historical-baseline.json`과 `before/docs/build/baseline.json`에 보존된다. 이전 final-verification/command/XML/receipt와 전체 이전 handoff 사본은 보존했다.
- Windows/Player/수동 화면·포인터·IME 미실행, standalone OFL NOT_CONFIRMED, coverage NOT_MEASURED. graph 갱신 없음: code STALE / semantic UNKNOWN. 첫 두 generator raw 비교 UNKNOWN 및 nonempty component cross-reference 일반성 미검증을 유지한다.
- model/effort 독립 provider 확인은 UNKNOWN. Astra/low는 발진 script·started journal로 확인되는 harness 설정일 뿐이다. 같은 agent 재개, 라우팅 변경 없음.
- 검사기는 기존 `tools/plan.py`, `render_plan.py`, planlib 및 jsonschema를 사용한다. 후행 story 시험 실행기나 새 수용 프레임워크를 만들지 않았다. public tool/schema/정본 acceptance/graph/Git index·commit·remote는 이번 수리에서 변경하지 않았다.
- 현재 다음 동작: Root 인계 판정 대기. 자동 agent/빌드/다음 story/외부 업로드 없음.

이 문서의 이전 원문은 `docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4/before/docs/agent-handoffs/CONTEXT_INDEX.md`에 있다. 과거 준비 단계 문장을 현재 상태로 해석하지 않는다.
