# Findings

- Platform native package explicitly preserves frame/glass stationary and does not claim handrail completion.
- Exact original tread source/world reconciliation already exists; seven native batch mappings, source-to-world matrix and source object IDs can be reused as navigation evidence.
- `PlatformEscalatorRun` samples `EscalatorMotion.Drive.TravelDistance`; it advances no independent clock. `EscalatorRide` reads the same drive's running state/speed.
- Canonical rules require real part/path binding and prohibit fabricated unseen return geometry.
- Archived extraction retains front-face material/smoothing/per-loop UV, source IDs, local/world matrices; back-face-specific materials and standalone curves are outside the extraction.
- Bounded Blender library inspection loaded 270 mesh descendants for 30 exact frame siblings (15 lanes, two sides), spanning 13 unique source mesh definitions. Native g0 geometry is a 0.09m wide elongated round-ended strip around separate glass; it has 280 gray70 triangles and 44 gray80 triangles. Candidate segmentation remains under inspection; material alone is not accepted.
- Source gray70/80 materials are untextured solid dark colors; gray100 is pure black but only 14 direct parent triangles. Shader UV offset on these native flat materials would not by itself produce visible motion.
- First plotting attempt lacked matplotlib; Pillow rasterisation of actual source triangle projections is available. The first overview accidentally includes glass in its middle panel due to a suffix filter; replace with exact source-path matching before use as evidence.
