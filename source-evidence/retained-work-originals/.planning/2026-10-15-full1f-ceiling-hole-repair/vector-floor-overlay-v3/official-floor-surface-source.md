# Official horizontal surface source

Complete read-only extraction from fresh V3 official meshes. `official-floor-surface-source.json` contains 163 object/material/height histogram rows and 14 significant surface groups with actual indexed-triangle union polygons, exact world XZ loops/holes and raw world triangles. Source-pixel loops sample these exact world loops at <=0.5 MODEL spacing through the existing mapper. No hull, bounds-derived contour, Unity call, scene edit, validation or floor edit.

Search: world Y6–8, prioritizing6.4–7.4; world face normalY>0.995. Height bins are triangle-centroid Y rounded to0.001 MODEL. ROI is source x50–365,y0–300. Histogram area is actual triangle area for whole triangles whose centroids fall inside ROI; it is explicitly **not** a partial-area clip. Exported full material/height groups retain their complete actual polygons outside ROI too, avoiding artificial ROI borders.

| Actual object / submesh | Y MODEL | Indexed area with centroid in ROI MODEL² | Material / meaning |
|---|---:|---:|---|
| OfficialStation_-부산역_선로상층부_0 / SM123 |6.800|4169.217|`옥상방수#004`: explicitly roof waterproof; exclude from floor authority |
| same / SM119 |7.000|483.206|`타일P#022`: actual tiled horizontal patches, circulation/platform floor candidate |
| same / SM87 |7.000|40.891|`타일P#023`: narrow service-side strip near source x354–359,y71–281 |
| same / SM117 |7.000|17.530|`타일P#010`: fragmented tiled patches |
| same / SM82 |7.000|14.440|`Material`: ambiguous isolated horizontal patches |
| same / SM116 |7.000|13.129|`보도블럭_맹인#002`: tactile paving strips |
| same / SM118 |7.000|8.093|`타일P#002`: narrow peripheral tiled strips |
| OfficialStation_-부산역_6 / SM34 |7.200|30.689|`source-4d5162a8119a6fa90240`: horizontal surface, floor/roof semantics unresolved |

Neighboring lower surfaces are also retained: platform-upper SM74 Y6.100,130.837MODEL², material `회색조 80%`; Layer0_5 SM26 Y6.086,51.256MODEL², source material `source-c084a52adea425d6ec29`. Their height/material does not establish the intended 2F slab.

The largest Y6.8 roof group comprises long platform strips, not a public-hall slab. Example actual source loops span from roughly x−260 to350–354 across bands y64–96,116–163 and234–279. These must not be used to justify V2 north/west floor protrusions.

Y7 tile/paving geometry supplies real retained floor candidates, but the unions are fragmented around west/peripheral paths and the service side. No broad upward near-horizontal retained official floor face was found matching V2's large public-hall envelope x66–313,y4.5–279 within Y6–8. This absence in the exported official meshes is not evidence that the real floor is absent. North glass at y≈41 and west glass aroundx79 remain facade evidence; this horizontal-source extraction does not promote them into a complete floor polygon.

Root owns contour choice. Read the separate actual floor candidates and roof candidates from JSON rather than merging all horizontal surfaces.
