# Findings

Input authority: RASTER-SIGNS-2F.json; source textures immutable. Geometry must preserve source contours and holes; no TMP or textured planes.

Source contact sheet inspected via view_image: 36 sources include photographic menus, TV imagery, four photographed product cards, and real logo/header artwork. Many source glyphs are blurred/subpixel and source-photograph content prevents claiming surveyed or fully legible reproduction. Continuous contour construction will preserve observed shapes without invented replacement fonts.

Source UV inspection: all47 faces have exact source UV bounds[0,0,1,1]; vector packet records full visible source crops. No unseen photo margins vectorized.

Root-directed complexity correction: dominant source backing color is one closed slab z0..0.12. Actual other source-color contour solids use z0.12..1, preserving letter counters through ink with backing visible. Strong glyph boundaries survive edge-preserving filtering; photographic paper/food grain is merged. The tall Soban source uses median9 only inside its observed food-photo rectangles; all menu typography remains outside those regions.

Construction-art inspection caught low-contrast lettering loss in the initial low-complexity pass (notably American Trailer menu0 and BNC title). Corrected filtering retains actual dark/light source-local strokes before denoise, protects their connected contours during close-tone merging and restricts palette centroid coalescing to distance6. Full-size rebuilt previews inspected: American Trailer menu93k triangles, Qamart116k, BNC47k. Dense Soban278k is a typography-priority exception rather than a reason to remove observed letters. No invented glyphs.

Root follow-up explicitly requests clipping four Hwang card artworks to actual observed card boundaries, excluding tray/background/neighboring-card photography and filling the existing physical face UVs. Original source images0 and3 are already cropped at their left edge; missing artwork stays source-limited, not invented. Root agreed to visible-only extraction. Mapper source_uv_bounds remain0..1; original source crop/quad metadata is separate.
