# Actual-source contour relief preparation

Authority is the root-owned RASTER-SIGNS-2F.json inventory: 36 unique runtime source textures, 47 existing face instances. Every face uses source UV bounds [0,0,1,1]. Exact input asset path, SHA-256, source pixel size, UV bounds and construction key are retained in the packet. Original source images are immutable.

Construction uses continuous directed source-color boundary rings, bounded simplification below one source pixel, hole-aware Mapbox Earcut caps and closed extrusion walls on every outer/interior ring. One continuous backing slab uses z0..0.12; actual remaining source-color contours use z0.12..1. Materials need solid source colors only. No scene text component, font atlas, textured image plane or pixel-cube mosaic is used.

Source photographic grain and gradients are approximated by up to12 principal colors (up to8 for larger sources), close-tone merging and edge-preserving denoise. The tall Soban menu additionally uses median filtering within its observed food-photo cells; type regions lie outside those rectangles. Source artwork preserves observed Korean/Latin glyph shapes, prices, rules and logo forms; no invented replacement contents.

The VECTOR preview files are polygon-art construction previews, not Unity captures, runtime checks or digital-twin fidelity proof. Blurred or subpixel source glyphs remain limited by original image resolution. Photo-proportioned sign dimensions, existing scene positions and source epoch are not surveyed measurements. Root performs scene application under its C180 authority; this lane writes only this vector-2f directory.

Tooling dependency: Mapbox Earcut Python2.1.0, ISC license, installed locally under deps with its distribution metadata/license. Original source artwork attribution/licensing remains with existing source assets and their authorities.


## Hwang four-card follow-up

Only actual-product-card-0.png through actual-product-card-3.png were reprocessed after the completed full pass. Their observed printed-card quadrilaterals are rectified to normalized artworkUV0..1, filling the existing physical sign face. Surrounding tray, neighboring sign, background and transparent-frame photographic bleed are excluded. The existing face silhouette stays fixed: any excluded rounded corner receives sampled actual paper color. Original source images remain unchanged.

Mapper source_uv_bounds remain [0,0,1,1]. Each corrected entry separately records source_art_crop_uv_bounds, original source pixel quadrilateral and clip polygon, homography, original source_size and artwork_raster_size. Thus original-photo crop metadata does not shrink the root mapper's face region. Source0 and3 already truncate the left card edge; missing printed artwork is neither reconstructed nor invented.

ACTUAL-SOURCE-RELIEF-INDEX.json indexes final individual t00 through t35 JSON files for bounded native imports. The other32 texture entries and all source assets remain unchanged. The final packet SHA-256 is recorded in CONSTRUCTION-SUMMARY.json.
