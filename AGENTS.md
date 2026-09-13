## PM orchestration entrypoint

- Current work instructions are GitHub issues and `docs/context/work-orders/NNN.json`. Start with `node scripts/context/task_context.mjs brief --issue <number>`, then read only the selected phase inputs/writes/checks before acting.
- The user is PM; members and their own LLMs execute bounded tasks. No personal preassignment, location routing, school-PC requirement, or high-spec ownership gate. Preserve #70’s evidenced existing claim.
- The ontology is `docs/context/work-orders/index.jsonld`; phase artifact dependencies are not whole-issue close-order. The technical graph is a detailed contract, not a request to load all context.
- Follow `docs/context/orchestration-contract.md`. No automatic reviewer/rework loop; keep existing protected review and data/permission boundaries.

# Agent rules for choo-guard

## Current execution contract (2026-09-12)

- Use `docs/context/pm-execution-contract.md` and the per-issue `docs/context/work-graph.json` as the current planning/ownership/dependency contract. The current target is 13 regions, 20 clients including instructors, 100 NPCs and 2 concurrent incidents; it is not a completed acceptance claim.
- No school/location/high-spec routing and no advance named assignees. Preserve an existing evidenced start. Claim precise scopes and leases only at actual start; independent checkouts may run independent work.
- Local-unpublished Native files are not a team checkout baseline. Consume #120's qualified reachable ref only where needed. Existing legacy prototype and receipt scope remain historical facts.
- Higgsfield/external-generation authority conflicts are SC-01: do not infer a resolution from timestamps or resume new requests until the scoped PM decision. Other Native work is not globally blocked. CV/SOTA reconstruction is rejected current work; retain old files/receipts as history, not instructions to restart.

## Source of truth

Git-tracked C#, tests, declarative scenario data, Editor builders, and SceneBundle manifests are the source of truth. Do not rely on an unrecorded Unity Editor state.

## Required loop

1. Read the issue and acceptance criteria.
2. Make one bounded change.
3. Add or update tests first.
4. Refresh Unity and wait for compilation.
5. Read the console and require zero errors.
6. Run relevant EditMode and PlayMode tests.
7. Validate scene hierarchy and serialized assets.
8. Capture evidence outside MCP Play Mode screenshot paths.
9. Review the complete Git diff before proposing a commit.

## Unity rules

- Use the official Unity CLI Editor MCP (`unity mcp --project-path <absolute-project-path>`) with the official `com.unity.pipeline` package. The 2026-09-08 PM decision supersedes CoplayDev/unity-mcp. Native MCP clients connect directly; pi-mcp-adapter is optional for Pi only. See `docs/adr/0006-official-unity-editor-mcp.md`.

- Do not hand-edit `.unity`, `.prefab`, `.asset`, or `.meta` YAML.
- Prefer idempotent Editor builders and project-scoped MCP tools over repeated ad-hoc hierarchy mutations.
- One write agent per Unity Editor. Other agents must be read-only.
- Pin all Unity packages and Git dependencies. Never track `main` or `beta` package branches.
- Do not retry a timed-out MCP mutation until current Editor state is re-read.
- Keep arbitrary C# execution (`execute_code`, `eval`, `eval_file`, and equivalent tools), external asset generation, and remote package installation disabled unless a human explicitly approves them.

## Safety and data

- Never commit raw captures, faces, vehicle numbers, sensitive signage, model weights, credentials, `.ulf`, `.env`, PLY/SPZ/GLB files from restricted facilities, or Unity build secrets.
- 2026-09-08 사용자 결정으로 철도 사진·복원 기하의 외부 3D 생성 서비스 전송을 일괄 금지하던 저장소 규칙을 폐기한다. 해당 자료를 외부 3D 생성 서비스의 입력으로 사용하는 방향을 허용한다. 이는 공개 Git 재배포, 제3자 저작권·개인정보·서비스 약관의 면제를 뜻하지 않는다.
- 코딩 LLM에 이미지를 첨부하지 않는 별도 사용자 지시는 유지한다. 3D 생성 API의 자료 입력과 LLM 대화의 이미지 첨부를 구분한다.
- Never change scoring rules, safety steps, coordinate conversion, or collision boundaries without tests and lead review.
- LLM output is not an authority for railway procedures or physical safety.

## Accepted visual and simulation direction

- Match real facility layout, equipment placement, routes and authorized emergency procedures as closely as available evidence supports; simplify appearance only. Unreceived facility/manual references remain provisional, not invented as verified.
- As accepted on 2026-09-07, the flat-only requirement is superseded. Use traceable public station references to model realistic structure, manufactured detail, material response and lighting in Blender/Unity. Keep observed features separate from estimated dimensions and unverified current layout; see `docs/art/public-station-references.md`.
- Every visible asset family must have an individually inspected image reference in `foundation/art/object-references.json`, concrete observed features, an authored source module/component binding and actual scene usage. A broad station photo list or an FBX count is not per-object reproduction evidence. Distinguish station-observed forms, manufacturer proxies, virtual guidance and structural adapters.
- Compare native front/side detail views with those references before marking visual review complete. Bind that review to the current FBX hash; preserve limitations on measured layout and institutional procedures. Asymmetric import, normals, moving colliders and visual ground contact require direct geometric tests when changed.
- Modeling, rendering, baking and development may run in any capable environment. Record measured capability and test scope; never require a school PC, named host, location or high-spec ownership for task assignment. External-generation policy remains subject to SC-01 and the independent data/rights/cost boundaries.
- Desktop first, later VR uses the same spatial anchors and action meaning. Gameplay objectives use world navigation cues; identical faceless evacuees follow the player without emotions or role substitution.
- The target connected world includes rolling stock, rails, platforms, terminal, ticket halls, station buildings and connected metro areas. Track evidence/coverage and operator boundaries per region. Public wayfinding diagrams and historical plans are not current dimensioned construction drawings.
- Historical reconstruction outputs retain input order/hashes, raw outputs and transform provenance. This preservation rule does not restart rejected CV/SOTA work. Public/native modeled geometry must distinguish observed structure from estimated dimensions; uncalibrated surfaces are not validated metric SceneBundles. XR target acceptance remains separately scoped.

## Continuous field training direction

- The primary flow is ordinary station operation with unforeseen, constrained incident episodes, response and recovery in the same world. Do not expose a scenario picker, onset countdown or undiscovered solution markers as the default experience.
- Randomize reviewed incident variations, never invent railway procedures randomly. Keep world state separate from what the player has observed. Equipment availability, routes and NPC behavior must reflect the same state.
- Preserve seed, profile and observed actions for replay/debrief. Synthetic completion and automated tests do not establish validated digital-twin fidelity or transfer to field performance.

## Portable project context graph

- Start with `node scripts/context/work_graph.mjs validate` and `node scripts/context/work_graph.mjs brief --issue <number>`. Read only the returned scoped sources at their declared stage. The legacy topic/machine brief is an optional historical-source explorer, not the current issue schedule or a location policy.
- `docs/context/project-context.json` is the Git-shared context index. It connects accepted and superseded decisions, code/assets, constraints, evidence, open questions and dated delivery snapshots. The graph never overrides its canonical sources or a newer explicit user decision.
- Record context changes in the same work unit as the affected implementation. Review source changes before refreshing hashes; preserve historical evidence and its covered hashes. A changed hash means reread, not an automatically renewed PASS. An external snapshot never proves current GitHub status offline.
- Keep paths relative and context portable. Never put machine-private state, credentials, confidential source documents or reusable publication permissions in the graph. Agent handoffs must include scope, touched nodes, evidence limits and next files to read.
