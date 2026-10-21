# Actual current-object 2F vector CAD authoring

Scope: read current physical scene mesh geometry under the exact C180 FIFO/nonce policy; no Scene/Assets mutation, saving, validation, Play, probe, capture or reopen. Source renderers, shared mesh identity, materials and localToWorld transforms remain individually mapped. Original source images are not used as metric geometry. Global current pointers belong to the root orchestrator.

Current first snapshot has actual 29 R25 shop roots. Its Scene is dirty before and after the read, so it is an editing-state source, not a proof that every live mesh equals the saved Scene file. The saved Scene SHA is recorded unchanged within the native call.

A fresh snapshot must use a unique tag, only after the root has finished the intended scene authoring/save:

```sh
python3 .state/ACTIVE-ALL2F-COMPLETION-R26/cad/export_snapshot.py --tag 003
python3 .state/ACTIVE-ALL2F-COMPLETION-R26/cad/build_current_2f_cad_r26.py --snapshot .state/ACTIVE-ALL2F-COMPLETION-R26/cad/snapshot-003/CURRENT-2F-MESH-SNAPSHOT.json --out .state/ACTIVE-ALL2F-COMPLETION-R26/cad/output-003
```

The native call is serialized and may not be preempted/reclaimed on timeout. An UNKNOWN stays reserved until actual exit evidence, per the existing broker. The wrapper never rewrites or replays a prior request/source snapshot.

DXF: meters; one unique locked achromatic layer per actual renderer. Source metadata distinguishes official fixed references, existing current2F, current R25 MODEL (no new user confirmation), and other matching-level references. JSON additionally stores per-height actual sections and projected physical contours. Native references use actual matching-height sections and Y7 surfaces; full-height roof/exterior silhouettes do not become 2F occupancy/floor authority.

Physical model footprints come from exact mesh triangle projection into the 6.97–14.03m band, not bounds/hulls. Coplanar triangle diagonals disappear through triangle unions; collinear touching section segments merge. Curved boundary vertices/interior holes remain. Derived physical error bounds are0.1mm for architectural parts and1mm for dense goods/furniture, constrained by every actual shared-mesh transform. Only the CAD changes; every original Scene/Asset vertex remains fixed. All 72 alternative LOD1 IDs are excluded; original LOD0 remains once. Fixture/source sign meshes remain actual mesh geometry; deleted Light/Text components are never created.

PDF pages contain no added visible text. New current model geometry is achromatic but does not become user confirmed through this export. The reader owns validation.
