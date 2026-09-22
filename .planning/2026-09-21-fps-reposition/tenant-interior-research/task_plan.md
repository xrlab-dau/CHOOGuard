# Tenant Interior Source Investigation

## Goal
Find and directly inspect high-value public sources for real Busan railway station tenant interiors and public-space topology, then deliver a provenance-rich source graph and bounded reconstruction strategy without changing Unity or production assets.

## Scope
- Target: KORAIL/KTX Busan Station at 부산 동구 중앙대로 206, not Busan Metro station 113.
- Prefer original tenant, architect, contractor, public procurement, and operator records.
- Retain 5–8 inspected sources or documented negative candidates; avoid link-list aggregation.
- Download only source PDFs/images into `asset-library/research-public/2026-09-21/fps-research/interiors/`.

## Phases
- [complete] Establish current evidence and candidate tenant/floor list.
- [complete] Search official procurement/operator and original design/contractor sources.
- [complete] Inspect candidate drawings and reconstruction media; acquire allowed files with hashes.
- [complete] Write and validate `tenant-interior-source-graph.json`.

## Evidence Rules
- Separate plan/elevation/render/photo/video and exact-branch confidence.
- State floor, date, dimension readability, and usable registration anchors.
- Treat photos and guide diagrams as dated inference inputs with explicit scale uncertainty.
- Never infer surveyed metre coordinates from an unscaled drawing.

## Errors Encountered
| Error | Attempt | Resolution |
|---|---:|---|
| Broad `find ..` stalled in a large tree | 1 | Interrupted and switched to bounded project/config paths. |
| KORAIL Retail legacy `fileDown2.do` returned HTTP 200 with zero bytes for two 2022 attachments | 1 | Treating attachment metadata as primary catalog evidence while testing static-path and archive alternatives; zero-byte placeholders will be removed/replaced. |
| Used zsh special variable `path` in a probe loop, hiding command lookup | 1 | Switched to a task-specific variable name as required. |
| ImageMagick contact-sheet labels failed because no default font resolved | 1 | Retrying without labels; filenames remain ordered numerically. |
| A second montage attempt still required a font | 2 | Built an unlabeled ordered sheet and inspected the original source images directly. |
| Four KORAIL Retail attachment attempts yielded zero-byte or HTML error responses | 2 | Removed invalid placeholders; retained exact catalog identifiers and acquisition gaps in the graph. |
| YouTube metadata retrieval succeeded but ranged media download returned HTTP 403 | 1 | Recorded the verified 00:41-11:20 interior chapter and left exact landmark timestamps as an explicit frame-acquisition gap. |

## Outcome
- Found and inspected a 2021-10-25 Grid-A B&C proposal package with a dimensioned 1F floor plan, RCP, elevations, and an escalator/fire-door adjacency anchor.
- Preserved the conflict between the original sheets' 1F label and the portfolio page's 2F metadata; classified the package as proposed/local geometry pending a current independent match.
- Acquired 34 original portfolio images and verified every file against `SHA256SUMS`.
- Delivered an eight-source, three-floor coverage and reconstruction graph. No station-wide measured interior plan was found.
