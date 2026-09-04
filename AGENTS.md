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
