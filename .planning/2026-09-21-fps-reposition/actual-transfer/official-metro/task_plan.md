# Official HUMETRO Busan Station topology research

## Goal
Establish from official or otherwise primary HUMETRO/Busan sources the current Busan Station (Line 1, station 113) internal public-route topology: platform-to-concourse vertical circulation, exit numbering, underground-shopping-mall connection, and the endpoint that interfaces with the 2019 Busan Station railway connection passage.

## Scope
- Read-only web research and evidence capture.
- Write only under this directory.
- Treat `fps-metro-import-graph.json` as navigation, not authority.
- No Unity, Assets, scenes, code, configuration, Editor interaction, or outbound messages.

## Evidence classes
- `directly_supported`: exact official page/image/plan supports the claim.
- `contradicts_premise`: official evidence conflicts with an existing modeled assumption.
- `near_match_only`: related official evidence without enough detail for the exact segment.
- `insufficient`: no primary plan found or topology cannot be resolved.

## Phases
1. [complete] Locate current official station pages, downloadable maps, and source metadata.
2. [complete] Trace exit-renumbering history and the underground shopping mall / railway-passage interface.
3. [complete] Extract plan topology and vertical-circulation facts, with image coordinates where applicable.
4. [complete] Produce `source-graph.json`, save source visuals, validate JSON and file hashes.
5. [complete] Hand off confirmed segments and critical gaps to the root agent.

## Stop condition
Stop when the official evidence confirms the usable path segments and clearly isolates any unconfirmed segment, without extending into route-video research owned by another lane.

## Errors encountered
| Error | Attempt | Resolution |
|---|---:|---|
| Project `docs/CODEX-NAVIGATION-GUIDE.md` and local `AGENTS.md` were not found in the current checkout | 1 | Used the supplied global instructions and confined work to the assigned lane. |
| Web reader returned internal errors/timeouts for the HUMETRO station pages and Busan City article | 1 | Switched to direct HTTP retrieval and will preserve response metadata locally. |
