# Delete all scene lights

## Goal
User: 지금 조명때문에 리소스 사용량이 너무 무거워 조명 전부 삭제해줘
Remove every Unity Light from the current FpsStation editing scene, including inactive lights, directly save, and retain exact deletion records. Preserve non-light geometry and original source Assets.

## Phase 1: Authority and scope
Status: complete
Read current AGENTS, C180 policy/broker and writer state; user authorizes all current scene lights. Existing writer released. Planning skill applied.

## Phase 2: Serialized removal and save
Status: complete
Create one bounded C180 constructor with exact grant guard. Delete light-only objects and Light/associated additional-data components on mixed geometry objects; preserve geometry and children. Save current scene, no QA/reopen/Play/captures.

## Phase 3: Actual receipt and handoff
Status: complete
Read constructor/broker result, record tombstones/current pointer, update project authority and complete plan. Do not claim performance measurements.

## Errors
- docs/CODEX-NAVIGATION-GUIDE.md absent; current AGENTS and canonical C180 files used.
- Initial optional broad reads truncated; replaced with scoped reads.

- Attempt 001 compiled but returned settled runtime failure at first Light remaining after DestroyImmediate. No save/actual completion receipt. Preserve in-memory partial state; diagnose actual target/prefab ownership and recover in fresh scoped request.

- Attempt 002 settled failure with exact cause recorded: UniversalAdditionalLightData on SHOP09 Source_Interior_Warm_Light_0 has hideFlags 64; remaining 103 lights. Request 003 clears only bit64 on the exact lighting components/leaf objects being deleted, preserving all mixed hierarchy geometry. Original first49 removals remain; no history undo used.

## Actual completion
2026-10-19T09:02:42.2091380Z Request003 SETTLED_SUCCESS. 152 total scene Light components removed and saved (49 earlier + 103 final call). Constructor receipt and exact tombstones retained. No Scene QA, Play, reopen, probes or captures.
