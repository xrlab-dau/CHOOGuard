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
- Do not send railway imagery or reconstructed geometry to external LLM or asset-generation APIs.
- Never change scoring rules, safety steps, coordinate conversion, or collision boundaries without tests and lead review.
- LLM output is not an authority for railway procedures or physical safety.

## Accepted visual and simulation direction

- Match real facility layout, equipment placement, routes and authorized emergency procedures as closely as available evidence supports; simplify appearance only. Unreceived facility/manual references remain provisional, not invented as verified.
- As accepted on 2026-09-07, the flat-only requirement is superseded. Use traceable public station references to model realistic structure, manufactured detail, material response and lighting in Blender/Unity. Keep observed features separate from estimated dimensions and unverified current layout; see `docs/art/public-station-references.md`.
- Keep heavy modeling, offline rendering and light/texture baking on the school PC. Lightweight local mesh generation and real-time visual checks are allowed. Never upload railway imagery or reconstructed geometry to external generation services.
- Desktop first, later VR uses the same spatial anchors and action meaning. Gameplay objectives use world navigation cues; identical faceless evacuees follow the player without emotions or role substitution.

## Continuous field training direction

- The primary flow is ordinary station operation with unforeseen, constrained incident episodes, response and recovery in the same world. Do not expose a scenario picker, onset countdown or undiscovered solution markers as the default experience.
- Randomize reviewed incident variations, never invent railway procedures randomly. Keep world state separate from what the player has observed. Equipment availability, routes and NPC behavior must reflect the same state.
- Preserve seed, profile and observed actions for replay/debrief. Synthetic completion and automated tests do not establish validated digital-twin fidelity or transfer to field performance.

## Portable project context graph

- Start each teammate/subagent work session with `python3 scripts/context/context_graph.py validate`, then `python3 scripts/context/context_graph.py brief --topic handoff --machine local` (use `art` or `runtime`, and `school-pc` when relevant). Read the returned canonical source files before changing their surface.
- `docs/context/project-context.json` is the Git-shared context index. It connects accepted and superseded decisions, code/assets, constraints, evidence, open questions and dated delivery snapshots. The graph never overrides its canonical sources or a newer explicit user decision.
- Record context changes in the same work unit as the affected implementation. Review source changes before refreshing hashes; preserve historical evidence and its covered hashes. A changed hash means reread, not an automatically renewed PASS. An external snapshot never proves current GitHub status offline.
- Keep paths relative and context portable. Never put machine-private state, credentials, confidential source documents or reusable publication permissions in the graph. Agent handoffs must include scope, touched nodes, evidence limits and next files to read.
