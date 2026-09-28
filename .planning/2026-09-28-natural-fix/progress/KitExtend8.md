# KitExtend8 — complete

- Implemented sloped `Canopy.geometry.vertexY` for planar plaster soffits with matching underside/top, vertical thickness, slope-following closed edges, and collider control in `AgentScripts/MajibangBuilder.cs`.
- Added `gypsumLit` derived material (gypsum source map; albedo [.90,.90,.89], emission [.30,.30,.29], smoothness .1, metallic 0); HallJunction uses it for both `colour` and `topFinish`.
- HallJunction contract and parameters: `.planning/2026-09-28-natural-fix/kit-requests/HallJunction-1.done.json`; test and preview paths are listed there. `kit-spec.md` documents `vertexY` and material values.
- Evidence: feature KitValidate 0 errors / 12 collider-free triangles; invalid count and non-planarity rejected; collider-on 14 triangles; all 19 frozen specs unchanged (265 elements, 367,776 triangles, zero errors); KitPreview showed continuous moderate-gray underside with no auxiliary light.
- Scope decision: sloped `vertexY` currently supports plaster with `fascia:0`, exactly matching measured source soffit. Horizontal Canopy behavior is unchanged. No Unity scene changes or saves.
- Next: Main integrates the requested element; no KitExtend8 work remains.
