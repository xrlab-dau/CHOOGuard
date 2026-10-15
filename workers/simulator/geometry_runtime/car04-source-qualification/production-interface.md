# Car04 production interface

`Car04InteriorAccess` consumes the frozen `Car04Access` resource owned by coverage. The source body remains `car:4`; the only door IDs are `cabin-door:4:near` and `cabin-door:4:far`. No `door:4` is created or operated.

- `PlatformSeededSimulator.Prepare` first builds the existing7car navigation, then creates and attaches the optional resource adapter. Exactsource leaves are partitioned into copied meshes, opened, and assigned only their partial-leaf semantic bindings.
- `PlatformNavigation.AttachInterior` includes the resource/algorithm geometryhash and dynamic interior version in navigation identity. LocalCar04/gangway data uses all original train colliders in12×4.5×24m bounds, voxel .02m, radius .28m, height1.72m, climb.28m. No floor triangles/colliders are added.
- `ProjectDoors` includes both cabin entities in the same logical-state, recorded-navigation and physical projection lifecycle as the original7doors. Each leaf has its own linked state and closed-door carver. Core `open_door` / `close_door` actions drive source copiedleaf TRS.
- Replay projects recorded cabin link outcomes and exact recorded endpoint tokens; it does not resimulate physics. Header geometry identity includes the new resource through the final root-owned manifest.
- Initial generation projects cabin derived navigation fields before the accepted initial-state digest.
- `Dispose` removes only owned links/carvers/localNavMeshData and restores original source MF/MC references and material references before destroying owned derivative meshes.
- Source44seat IDs/positions/facings and aisle destinations come from frozen coverage. Seat approach means reaching the original aisle beside the source cushion. Seated pose/collider realism and surveyed seat labels are not asserted.

The exact .85m lateral movement is a bounded training animation into the existing source sidebay; the field door articulation/timing remains unverified. Source rack, exterior door, all original bodies/floors/sidewalls/jambs/materials are retained.

Canonical acceptance key: `playmode-probes.json.car04_passenger_access`, containing source_seat_count44, external_door_blocked, open/closed/restored routes with forward+reverse realcapsules, core actions, version changed/restored,44source seat approach paths, actualNPC→seat approach arrival/core completion.

Development staging deliberately retains the prior SourceManifest as diagnostic context; root owns the final complete canonical stage/fingerprint and validation.
