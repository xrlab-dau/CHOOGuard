# SourceCut progress

## Done
- Read plan; targets confirmed from geometry-after33: all 4 targets share matrix (scale 100, rot), readable assets.
  선로상층부_0 currently uses `Assets/ChooGuard/Art/StationInterior/Majibang/OfficialStation_-부산역_선로상층부_0_MajibangDoors.asset` (not the FBX) -> "original" = mesh assigned at first Apply.
- MainShell_5 wall near U 55-65: single-sided faces (front normal ~ +V), wall line v = c + .0722 u (box frame), c 7.866 (U 18.5-58.5) and c 7.465 (U 58.5-69.5), jamb at U 58.5, pilaster U 64.94-65.1. Floor plane Y 6.8.

## Decisions
- Clipping in station UVY (double), barycentric polygons, 6-half-space decomposition; untouched triangles keep original indices; original vertices kept (untouched submeshes byte-identical).
- Apply upserts openings by id into the manifest and rebuilds each affected object from its ORIGINAL; Revert(ids) removes ids (rebuild) or object paths / all (restore + delete asset).

## Next
- Write AgentScripts/SourceOverrides.cs, openings/test.json, run Preview, message Main.
