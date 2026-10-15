# 2F source discovery — 2026-10-15

Status: complete. Read-only source inspection; no Unity calls, Assets changes, scene changes, validation, or new agents.

## Exact clean architectural source

- Best clean preannotation raster: `docs/BusanStation_2F_Guide_20261009/official-source-maps-20261009/부산역_2층_원도배경_원본추출.jpeg`.
- Pixels: 631 × 496. SHA256: `49695f811421e97ea1b4a0b0c6f6657bf917a90730a0d9e19e152d1e1117caf6`.
- Original PDF: `docs/BusanStation_2F_Guide_20261009/official-source-maps-20261009/부산역_2층_매장위치도_20261007_원본.pdf`, page **1** of 2. Page 2 is Ulsan and is unrelated.
- PDF page 1 xref **36** embeds the exact same JPEG bytes and SHA256. Full page is 720 × 540 pt.
- Architectural raster placement on page: `[9.720000267028809, 114.4798583984375, 710.6399536132812, 531.119873046875]` pt.
- Source-pixel to PDF-page mapping: `pageX = 9.720000267028809 + 1.1108081669512717 * sourceX`; `pageY = 114.4798583984375 + 0.8400000295331401 * sourceY`.
- Use clean JPEG directly for source-pixel overlays. Its axes/canvas exactly match the 631 × 496 source; no page-margin mapping is required.

## Live drift in paths whose names imply clean originals

- `workers/twin/output/architecture-resume-20261010/MAIN2F-floor-1F-B2-registration/approved-source2F-49695f8114.jpeg` currently hashes `7f3739239634c48b9df61f4f9173356622ccca2b562bbe36662b1c930acb7ca4`, so its filename/stored metadata does not prove clean bytes.
- `workers/twin/evidence/floor2-architectural-pdf-search-20261009/official-100083811-store-plan-page-1-embedded-2.jpeg` currently hashes `728cd4122aa76fb8d22d8778bdc51a2f43a1c0ec081af5ce0bafe6e5e3d091ba`, also differing from the original.
- Preserve both current annotated files and use the clean docs copy above for linework.

## Native vectors versus authored reconstruction

- No original full 2F architectural vector source was found in retained drawing folders.
- The official PDF has 48 drawing objects on page 1, but architectural linework is the embedded 631 × 496 JPEG; the native PDF paths are tenant/color/callout overlays and page presentation geometry. They do not provide full architectural wall/boundary vectors.
- `docs/BusanStation_2F_CAD_20261010/BusanStation_2F_OfficialPlan_Editable_SourcePixels.dxf` and `BusanStation_2F_OfficialPlan_Editable_Preview.svg` are **authored raster reconstructions**, not original architect CAD. README explicitly says vectors were extracted/reviewed from the official 631 × 496 JPEG and generated locally with ezdxf. Metadata records 386 entities and reconstructed wall/column/core inputs.
- DXF coordinate convention: `X=sourceX`, `Y=-sourceY`, `Z=0`; one CAD unit is one raster pixel, INSUNITS=0.
- Manifest-referenced reconstructed source vectors: `workers/twin/output/floor2-raster-reconstruction-20261009/main/main2f-source-vectors.json` (currently absent) and `workers/twin/output/floor2-raster-reconstruction-20261009/hall/hall-architecture-source-pixels-v2.json` (present). These may provide navigation only; actual linework/current official mesh correspondence must determine the new contour.
- CAD layers are W_WALLS70, OPENINGS30, COLUMN_AXES97, COLUMN_GLYPHS105, COLUMN_NATIVE_PROFILES53, CORES26, X_NO_2F_FLOOR4, SOURCE_FRAME1. There is **no building/floor outer-perimeter layer**. SOURCE_FRAME is explicitly `image_coordinate_canvas_reference_not_building_perimeter` and is merely `[0,0]..[631,496]`.
- Reconstruction method: `main/build_vectors.py` reuses earlier manually classified wall/boundary runs, applies reviewed endpoint corrections, aligns already classified wall strokes locally within ±3px using dark-ink support, and buffers them using visually annotated raster stroke widths. It expressly does not classify all black ink as walls. `hall/build_hall_trace.py` uses explicit manually classified raster stroke coordinates with ±2px endpoint/thickness inference. `export_cad.py` serializes these existing reviewed vector inputs with ezdxf; it does not recover architect CAD paths.
- Hall JSON has four **inferred**, not original, `floor_boundaries`: H2-floor-public-hall; H2-floor-east-platform-concourse; H2-floor-service-spine; H2-floor-main-hall-seam. Their source basis is visible perimeter/continuous white floor region, planar inference ±2px. They are not exported as a full CAD floor perimeter.
- Relevant inferred west source strokes in Hall JSON include `[95,30]→[66,30]`, `[66,31]→[66,71]`, `[66,89]→[66,145]`, stair-middle outer `[67,148]→[103,148]→[103,165]→[95,165]→[95,197]`, and diagonal `[94,205]→[135,274]`. These differ from the approximate yellow markup and require clean-source interpretation and current geometry correspondence; they are navigation data, not new authority.
- Prior retained-source handoff (`workers/twin/evidence/20261010-missing-floorplans-deep-research/HANDOFF.md`) likewise identifies the full 2F source as 631 × 496 raster and leaves higher-resolution full 2F/boarding interfaces missing. 2026 goods-plan p5 is only a MAIN-context candidate with no floor label, so it cannot replace this source.

## Documented pixel correspondence (historical model frame)

Frame: `.planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/unified-frame.json`.

`[sourceX,sourceY,1] @ [[-0.10967428542396562,-0.2879851030285814],[-0.30794809973764947,0.11106535302671859],[135.45619186760297,52.647506373321896]] = [worldX,worldZ]`.

This is the documented historical MODEL registration, not independent measurement or current scene readback. The prior MAIN floor packet named in `CURRENT-MAIN2F-B2-RELATIVE-REGISTRATION-DIAGNOSIS.v2.json` is currently absent at its referenced path.

Planning state: parent plan owns overall task; this delegated source lane is complete. Skills read: `planning-with-files`; no competing root-plan edits made.
