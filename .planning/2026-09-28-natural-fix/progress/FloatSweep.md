# FloatSweep — read-only detector

## Milestone: detector implemented; scoped checks pass

- Generator: `.planning/2026-09-28-natural-fix/floatsweep/floatsweep.py` (`AUDIT_DIR OUT_JSON`). No scene/build/editor changes.
- Current baseline: `.planning/2026-09-23-video-twin/1f-implementation/geometry-after56` (1934 active renderers; SkyPlaza base+fitout and seam screen present; EdgeBand renderer contains bands 1-2 only).
- Detection: connected mesh components and 5 cm support distance; route-visible dark/flat materials; unguarded collider floor boundaries (same-level floor test, collider sections at +0.35/+0.9 m, modeled drop when available); conservative route-near door/glass roofed-void candidates.
- Main's after56 known-case expectations: band 3 absent; bands 1-2 unflagged; SkyPlaza deck supported by piers from apron; seam-led-screen supported on its closed black base. The detector validates these before completion.
- Evidence output: ranked report plus per-item CPU owner-pick/crop files in `<stem>-picks/`; picks reuse the indexed geometry, 24x14 pixels, 50 m centroid clip; not Unity scene renders.
- Scoped smoke: `py_compile` passed; synthetic route-near unguarded 3F boundary produced `dropM=7.0` and exposed-edge estimate 8 m²; east-extension point-to-spec mapping resolved the 2F bridge, exit-10 landing, and 3F slab to their respective receipt IDs; fail-closed validation passed its positive fixture.
- after50 contains 537 active non-trigger BoxColliders; all 13 whose bounds span 3F +0.4..+0.9 m are shop counters/kitchenette/fixtures, not edge guards, so the floor-edge detector's collider section check remains on MeshCollider geometry.
- After49 failed after ~606 s with a NumPy heterogeneous-sequence `ValueError`. After50 full run timed out at 3600 s before any report; profiling found 646,099 triangles with radius >0.75 m, so support search now uses radius-binned KD-trees instead of rescanning all large triangles for every probe.
- The earlier after56 attempt timed out at 3600 s without a report. The rerun (bg_410) adds grouped component-face extraction and prefilters collider triangles to those crossing each section height before `interior.cut`; stage timing is printed to stdout.
- After56 endpoint check from `.planning/2026-09-24-interior-twin/specs/full/sky-plaza-fit.json`: all 30 piers are rendered; every base is 0.004 m from apron and every top 0.016 m from deck slab, within the 0.05 m contact tolerance.
- After56 seam screen check: the 11.26 m² screen has all 22 support samples within 0.05 m of `연결 통로 설비/Kit_Board_Canopy_Dark` (closest 0.002 m); the floor is not its local support surface.
- Latest scoped checks: `py_compile` passed; synthetic guarded edge emitted no signal and the unguarded counterpart emitted a wall-less-edge signal after the collider section optimization.