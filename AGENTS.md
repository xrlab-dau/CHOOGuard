# Agent rules for choo-guard

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

- Do not hand-edit `.unity`, `.prefab`, `.asset`, or `.meta` YAML.
- Prefer idempotent Editor builders and project-scoped MCP tools over repeated ad-hoc hierarchy mutations.
- One write agent per Unity Editor. Other agents must be read-only.
- Pin all Unity packages and Git dependencies. Never track `main` or `beta` package branches.
- Do not retry a timed-out MCP mutation until current Editor state is re-read.
- Keep `execute_code`, external asset generation, and remote package installation disabled unless a human explicitly approves them.

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
- Keep heavy local modeling, offline rendering and light/texture baking on the school PC. Lightweight local mesh generation and real-time visual checks are allowed. 외부 3D 생성 서비스 사용에는 위의 갱신된 자료 정책을 적용하며, 종전의 일괄 전송 금지를 되살리지 않는다.
- Desktop first, later VR uses the same spatial anchors and action meaning. Gameplay objectives use world navigation cues; identical faceless evacuees follow the player without emotions or role substitution.
- The target connected world includes rolling stock, rails, platforms, terminal, ticket halls, station buildings and connected metro areas. Track evidence/coverage and operator boundaries per region. Public wayfinding diagrams and historical plans are not current dimensioned construction drawings.
- Reconstruction must preserve input order/hashes, raw camera/depth outputs, masks and explicit coordinate/scale transforms through PLY/GLB, Blender and Unity. Uncalibrated review surfaces must not be labeled metric SceneBundles or receive training collisions. VR hardware remains undecided.

## Continuous field training direction

- The primary flow is ordinary station operation with unforeseen, constrained incident episodes, response and recovery in the same world. Do not expose a scenario picker, onset countdown or undiscovered solution markers as the default experience.
- Randomize reviewed incident variations, never invent railway procedures randomly. Keep world state separate from what the player has observed. Equipment availability, routes and NPC behavior must reflect the same state.
- Preserve seed, profile and observed actions for replay/debrief. Synthetic completion and automated tests do not establish validated digital-twin fidelity or transfer to field performance.

## Portable project context graph

- Start each teammate/subagent work session with `python3 scripts/context/context_graph.py validate`, then `python3 scripts/context/context_graph.py brief --topic handoff --machine local` (use `art` or `runtime`, and `school-pc` when relevant). Read the returned canonical source files before changing their surface.
- `docs/context/project-context.json` is the Git-shared context index. It connects accepted and superseded decisions, code/assets, constraints, evidence, open questions and dated delivery snapshots. The graph never overrides its canonical sources or a newer explicit user decision.
- Record context changes in the same work unit as the affected implementation. Review source changes before refreshing hashes; preserve historical evidence and its covered hashes. A changed hash means reread, not an automatically renewed PASS. An external snapshot never proves current GitHub status offline.
- Keep paths relative and context portable. Never put machine-private state, credentials, confidential source documents or reusable publication permissions in the graph. Agent handoffs must include scope, touched nodes, evidence limits and next files to read.
