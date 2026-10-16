# CHOOGuard

Project instructions reset by explicit user request on 2026-10-15.

## Current scene workflow

The user performs validation. Prioritize geometry edits, direct scene application and saving. Do not run agent validation, save-reopen checks, probe checks, comparison captures or review stages unless the user asks for them. Preserve existing user edits and original source assets while making the authorized changes.

Placed official objects and current 1F columns are the primary coordinate authority. Keep their world positions fixed and locally deform drawing proportions to them; paper-to-paper alignment must not override scene geometry.

Preserve the user-approved floor and required opening topology. Fit inferred shafts, walls and doors to those existing boundaries; do not cut new floor holes to accommodate an inferred placement unless the user explicitly requests that floor change.

For 2F floor repairs, preserve the user-marked X voids as well as elevator, escalator and stair openings. Fill other floor holes inside the existing station perimeter, including column gaps and openings exposing platform ceilings.

When filling floor holes, keep the exact pre-edit Main floor exterior rings fixed. Other official-object surface unions, hulls, buffering or gap closing must not enlarge that exterior.

Floor-fill review overlays use vector CAD generated from current confirmed Unity object geometry. Do not use a stretched architectural drawing as the background. User marks are approximate location cues. Select each neighboring hole in its full extent from actual floor, wall and object boundaries; never use pen shape, stroke width, bounding boxes or marked endpoints to clip proposed fill areas.

Keep the restored OfficialStation_-부산역_5 wall faces. Resolve later overlaps by reducing generated Main geometry; do not reapply its retired 2F wall cut without explicit user direction.

## Checkpoint and cleanup state

Current accepted snapshot: `.checkpoints/checkpoint-1/CHECKPOINT.json`; restore instructions: `.checkpoints/checkpoint-1/RESTORE.md`. Live writer authority is `.state/WRITER-AUTHORITY.json`. Read it before a Unity or Assets mutation; serialize writes under a scoped root/author lease. Current requirements: `.state/REQUIREMENTS.md`. Original drawings/photos/videos and accepted mapping: `source-evidence/INDEX.json`, `asset-library/`, `workers/twin/evidence/`. Historical completed `.planning` and worker output trees were explicitly retired by the user; do not automatically restore them from Git or recreate their data.

## Current gameplay implementation

The user's accepted provider plan is `workers/simulator/docs/approved-play/`. The installed `Assets/ChooGuard/Seeded` runtime uses the user's selected subscription provider/account/model for whole-world initial causal priors and NPC plans. Existing JEV-only composition implementation notes apply to the legacy path. Preserve full object/state/parent/relationship input and use the causal kernel, typed plans and NavMesh feedback. Read `docs/current-station-play.md` for actual implementation limits, default fixed seed20261016 and per-PC helper/login requirements.

Keep FpsStation as the editing scene; explicitly open Bootstrap to test title mode selection and return to FpsStation afterwards. Do not rerun the dated installer on an already installed scene. Preserve current source geometry and asset GUIDs. Do not commit account files, capabilities, tokens or raw machine-local Runs.
