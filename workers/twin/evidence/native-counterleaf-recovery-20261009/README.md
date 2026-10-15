# Native counterleaf recovery: restoration rejected

**The semantic-root omissions are not full-scene absence.** Thirty-one exact original triangles already survive in the current native background renderer and its MeshCollider. The requested stop condition is met; no restoration FBX, operation controller, pose change or gate removal was produced.

| Counterleaf source ID | Source meshes / triangles | Semantic omission | Exact triangles already in background | Triangles with some coplanar coverage |
|---|---:|---:|---:|---:|
| `b1b76f4dc4212fc889f2fb81` | 16 / 1054 | 5 meshes / 58 triangles | 10 | 46 |
| `8229d80042f799f775fb53e2` | 16 / 1054 | 2 meshes / 22 triangles | 11 | 22 |
| `ebd140c8843a960da0c9681a` | 16 / 1054 | 5 meshes / 58 triangles | 10 | 46 |

All 31 exact matches lie in `FPSWorld/공식 자료 부산역 역사/PlatformsParking/OfficialStation_-부산역_선로상층부_0`, currently using `Assets/ChooGuard/Art/Fps/NativeAutomaticDoors20261009/native-door-render-background-2839370.asset` for visual geometry. Maximum exact corner difference is 0.000135553 existing model units. Source IDs and original triangle ordinals map to current background triangle ordinals individually in `whole-scene-absence-audit.json`. Renderer and MeshCollider searches independently give 31 exact matches / 114 triangles with some coplanar overlap. Coplanar overlap is often partial, including surviving clipped source fragments; 114 is not a count of fully preserved faces.

The whole-scene search used the latest after-busanok-finishes binary export, scene SHA `5febc30153d38e48a263399aa9658163c3ab1f75d4c0185f256d22d18b9f7654`, which matched the saved scene at audit start. All owner bounds were considered; spatially overlapping owners contributed 583665 renderer triangles and 588520 MeshCollider triangles. No unreadable candidate mesh remained. Inactive/disabled mesh owners were included in the absence search and recorded; matches shown above are active/enabled.

## Source and placement evidence

- `original-counterleaf-geometry.json` is an exact hash-qualified extraction of all three complete source leaves: 48 meshes / 3162 triangles, including the twelve omitted meshes / 138 triangles. It retains source identity/path/mesh identity, original authored matrices, triangle indices, per-loop UV layers/normals, smooth flags and material slots. It is source evidence, not an installation packet.
- The fixed existing source-to-current transform was checked independently against 36 surviving current source child meshes / 3024 triangles, without refitting. Maximum bidirectional vertex errors per leaf are 0.000083368 / 0.000077999 / 0.000071490 model units. This verifies archived geometry placement in the current map, not physical site accuracy.
- `native-material-resolution.json` resolves all twelve omitted parts / 23 ordered material slots to existing Unity materials by identical archived source mesh identity and ordered source material slots in the complete native paired partners. GUIDs, material hashes and current counterpart renderer paths are recorded.
- The authored mirrored ±110-degree configurations remain unchanged. The negative closing fixture in the earlier pair review is still rejected; no mechanical closure/pivot is inferred.

## Decision

Restoring twelve whole original parts would duplicate exact and clipped background geometry. A later production change must first identify and reconcile individual surviving clipped polygons and the corresponding original source polygons, preserving nearby static headers and infill. No whole-group deletion or nominal closed pose follows from this evidence. Root alone decides and performs integration.
