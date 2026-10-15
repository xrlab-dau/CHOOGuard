# Official boundary source extraction

Complete, read-only. Output: `official-boundary-source.json`, using fresh V3 current geometry and mesh binaries. It contains 42 object/submesh/height sections, 206 merged polylines, and exact Y3 section perimeters for C096/C097/C098/C099/C100/C113. No scene or Assets writes, Unity calls, captures, validation or floor contour edits.

All traces come from actual indexed triangles. JSON retains original triangle ordinals, object/hierarchy/mesh asset/submesh/material, exact unrounded world XZ and registered source-pixel section segments. Merged polylines join only intersecting section segments at 0.00001 MODEL quantization; disconnected bays remain separate. No AABB, convex hull or unused vertex prefix substitutes for an outline.

## South: two different arcs

The lower vertical facade lies close to the southeast column arc. Its actual contributing faces are vertical (absolute normal Y=0), with top near Y6.03996. At Y3 and Y5.9, repeated lower facade panels include:

- `OfficialStation_-부산역_3`, SM10, `source-e09541964655b603cdc9`: source `(424.86,451.92)` through the separate bays to `(522.04,430.88)`.
- `OfficialStation_-부산역_4`, SM13, same material: continuation `(416.61,453.10)` to `(424.58,451.97)`.
- `MainShell1F_Native4_i1388730_fixed_upper_glazing`, SM13, same material: explicitly named glazing pieces, source approximately x522.3–545.5, continuing the inner arc.
- Eastern lower facade facets: `OfficialStation_-부산역_1`, SM20, `source-12d8d8e11c8ed4a856be`, and `_0`, SM50, `source-9da34a1887593efd304c`. Exact pieces appear in JSON; these facets are not silently bridged across entrances.

There is also a farther **outward sloped band**, using `source-a280c283311849b2fbb7` on `_2` SM14, `_1` SM11 and `_4` SM6. Example Y5.9 source points are `(494.29,453.09)`, `(551.38,437.54)` and `(582.82,427.03)`. At Y7.1 it shifts farther outward. This is a different source-model surface, roughly 15–20 source pixels south of much of the lower facade/column arc. It must not be substituted for the lower wall/glazing contact by accident. Current material queue2000 alone does not identify it as floor, wall or roof; JSON labels it geometrically and preserves both traces for root choice.

Southeast column navigation averages from indexed geometry are approximately C100 `(425.97,448.64)`, C099 `(450.97,444.21)`, C098 `(475.27,438.94)`, C097 `(500.10,433.19)`, C096 `(524.48,425.57)`, C113 `(548.79,418.32)`. Averages are navigation only; JSON includes actual perimeters for contact decisions.

## West

- `OfficialStation_-부산역_5` has actual MAIN diagonal facade sections at Y3/5.9/6.9 around source `(60.3,304.2)→(69.8,320.0)→(125.1,409.1)`, split across exact source-model faces and materials. JSON preserves all candidates in this bounded west MAIN region instead of replacing them with a diagonal approximation.
- Upper west x58–140,y0–310 comes from `OfficialStation_-부산역_선로상층부_0` under PlatformsParking. Selected submeshes3/10/11/23/26/41 at Y7.1 provide actual structural/facade/glazing candidates. SM26 material `P0_OPT2_TwoSided_s26` has transparent queue3000; others retain their individual material identifiers.
- These upper traces include separate bays, corridor/spine faces and platform structure. They do not establish one continuous outer floor edge. Roof/column/bay interpretations must remain separate; the trace source itself does not authorize filling exterior strips.

Root owns current first-floor boundary1f extraction, north trim, SW O/X/O and final floor contour. This lane is complete.
