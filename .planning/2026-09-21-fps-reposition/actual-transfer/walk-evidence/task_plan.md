# Actual Busan Station ↔ Metro Transfer Walk Evidence

## Goal
Establish one continuous, first-hand visual route (or record the access gap) from Busan Railway Station 1F/public entrance through the completed underground passage to Busan Metro, plus one independent contemporary visual reference. Preserve timestamps, observed landmarks, level changes, turns, signs, and unshown segments. Do not infer dimensions or geometry.

## Scope
- Owner: `actual-transfer/walk-evidence/` and its source graph only.
- Read-only external research; no Unity/editor/assets/config/source changes.

## Phases
| Phase | Status | Deliverable |
|---|---|---|
| 1. Recover route context and set evidence standard | complete | Existing municipal completion evidence reviewed; source graph scope set. |
| 2. Locate and inspect first-hand continuous walk footage | complete_with_access_gap | 2019 dated visual photo sequence inspected; 2019/2022 continuous candidates identified but their frames are blocked. |
| 3. Cross-check with an independent source and time conflicts | complete | 2021 operator-plan structure and later dated video metadata compared; current visual route remains unavailable. |
| 4. Publish compact graph and hand off | complete | `source-graph.json` and public-source receipts written. |

## Evidence standard
Only observed frames support waypoint claims. Metadata and captions support date/source identity only unless they show the place. Explicitly list gaps. Do not convert floor labels, exit numbers, or old model features into current facts without a dated visual source.

## Errors encountered
| Error | Attempt | Resolution |
|---|---|---|
| Project-local planning skill path absent | 1 | Used installed `/Users/um-yunsang/.agents/skills/planning-with-files/` instructions. |
| Googlevideo direct download was 403 | 1 | Use storyboard frames and public dated photo sequence instead of retrying same download. |
| YouTube TV player asked for reload | 1 | Do not repeat; inspect storyboard route. |
| YouTube storyboard tiles were 403 | 1 | Stop YouTube retrieval attempts; record its uninspected continuous-route candidate as an access gap. |
| Initial note patch did not match the current progress tail | 1 | Re-read scoped planning files and applied a context-correct patch. |
| 2022 transfer video media download was 403 | 1 | Do not retry the same stream; use only its dated metadata and inspect the accessible thumbnail as a one-frame source. |
