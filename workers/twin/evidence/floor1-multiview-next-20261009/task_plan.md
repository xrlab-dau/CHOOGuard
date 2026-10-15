# 1F new-source multiview architecture lane

Goal (user steering): acquire authoritative readable1F floor guide, extract architectural/semantic layout, register to current scene without claiming metric CAD, and deliver exact-owner layout corrections. Root owns Unity and Assets mutations.

## Scope and limits
- Own only workers/twin/{output,evidence}/floor1-multiview-next-20261009 and asset-library/research-public/2026-10-09/floor1-multiview-next.
- Browser only Aside CLI repl. No cloud reads, paid jobs, publishing, or nested agents.
- At most six new relevant sources and 500 MB acquired. Public data is reference_only; 0.02 m accuracy is unverified.
- Preserve source BOH 4919/4924, stair, and elevator cores. Failed old camera106/123/133 tracks are not repeated.

## Phases
1. Read prior failed depth audit, current source, protected recipes, project rules: complete.
2. New-source photo/video multiview search: stopped by user steering; source01 saved and source02 checkpoint parsed and saved.
3. Acquire actual1F floor guide through Aside; verify floor/date/source and extract guide semantics: complete.
4. Register normalized guide to exact current source footprints; produce annotated architecture layoutdiff/correction packet with offlineQA: complete.

## Stop condition
Stop after root-ready guideplate/layout packet with evidence/tolerances or documented missing data. Guide-based semantic layout restoration is authorized; keep metric/survey claims unverified. No Unity mutations in lane.

## Errors
Aside listTabs unsupported; use tabs/page. Cross-origin fetch failed; same-origin CDN navigation/fetch succeeded. Buffered running stdout was parsed too early; waited for completion.
