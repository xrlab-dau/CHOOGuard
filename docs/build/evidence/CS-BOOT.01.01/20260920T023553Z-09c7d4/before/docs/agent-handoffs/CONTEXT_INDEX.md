# CHOOGuard 문맥 인덱스

상태: **NEEDS_RECONCILIATION** — 문맥 준비 가능, TMP 7경로 scratch 관측·NUnit 2/2 PASS 완료; root 경계 검토 전 제품 작성 보류.

- 정본: `docs/CHOOGuard_Story_Plan_v4/plan.json` (`CHOOGUARD-ROLLING-STORY-PLAN-004`), GLOBAL_CONTRACT 및 basis/v3 기술 기준.
- 첫 story: CS-BOOT.01.01 / candidate / fixture. `CS-BOOT.01.01/PLAN.json`, `PLAN.md`, `CONTEXT.json`, `GENERATED_FILES.json`을 읽는다.
- 활성 장부: `docs/CHOOGuard_Story_Plan_v4/state/progress.json`; EXT-UNITY는 로컬 Editor/기존 라이선스만 SATISFIED. records/claims는 비어 있다.
- 검사기: `docs/CHOOGuard_Story_Plan_v4/tools/plan.py`, `render_plan.py`, `tests/test_plan.py`. 제품 Unity 시험 실행기는 후행 story 계획이며 아직 없다.
- graph: `graphify-out/graph.json`, undirected context graph, freshness UNKNOWN. 이전 탐색을 재사용하며 이번에는 질의·재생성하지 않았다. plan.json의 requires가 phase·artifact dependency 정본이며 partOf/similarity/confidence는 선행·승인이 아니다. 관계 매핑과 원문 raw hash는 CONTEXT_INDEX.json에 있다.
- TMP static-only 7경로와 metadata 15개의 실측/hash/의존성 고정; 원고지 bytes 보존. Windows build/run, 실제 제품 시험 NOT_RUN.
- 제품·graph·도구·스키마·Git index/remote를 변경하지 않았다. 무관한 .remember drift는 보존한다.
- 실행 증거: `docs/build/evidence/CS-BOOT.01.01/20260920T002730Z-b4c5b4`; compact `root-copy-candidate.json` 및 `verification.json`을 사용한다. 이전 증거는 보존했다.

## Revision 4 — Root implementation gate

`PLAN_READY`: Root reported inspection of all 61 actual candidate hashes, containment and missing root paths. Evidence: `docs/build/evidence/CS-BOOT.01.01/20260920T010925Z-20b06c/boundary-inspection.json`. The earlier pending-root statements describe revision 3, now superseded. Current canonical ownership includes 70 paths; root implementation and actual scene-missing RED are authorized. No ACCEPTED/SUBMITTED record.
