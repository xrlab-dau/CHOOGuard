# Current saved station world packet

`build_snapshot.py` freezes the **currently saved** `Assets/ChooGuard/Scenes/FpsStation.unity`. It reads source files only and invokes no Unity API. It does not read historical scenes, reconstruct deleted objects, import original scripts, or export arbitrary MonoBehaviour values.

Run from the repository root:

```sh
python3 workers/simulator/world_inventory/build_snapshot.py
python3 -m unittest discover -s workers/simulator/world_inventory -p test_snapshot.py -v
```

A completed snapshot is immutable. Rebuilding the same scene hash refuses to overwrite an existing receipt. `--output <new-parent>` permits a separate reproduction packet. Concurrent source writes fail the stability gate; obtain a new stable snapshot rather than rolling back the original scene.

The final packet is `output/simulator/world-snapshot/ec2f5f3b6097bcc7/`:

- `world-objects.json`: all 4,115 saved objects, including inactive objects, with stable Unity scene GUID/fileID identity, parent, local TRS, world matrix, flags and typed component identities. Geometry references are allowlisted. MonoBehaviours expose script identity and unsupported status only.
- `pack/Assets/WorldSnapshot/FpsStation.geometry.unity`: exact current GameObject/Transform/MeshFilter/MeshRenderer/BoxCollider/MeshCollider/LODGroup/Light documents, with original scene fileIDs, geometry component bytes, active state and source asset references. Every scene MonoBehaviour is removed and each GameObject component list is repaired. The original scene GUID is preserved in its copied meta; never stage the raw scene beside it with the same GUID.
- `dependency-closure.json`: recursive current geometry asset/meta GUID closure and shader includes. Source and copied hashes, sizes and original paths are recorded. Unity built-in references and the official URP Editor AssetVersion package prerequisite are explicit. No project executable dependency is copied.
- `runtime-alias-analysis.json`: saved KTX/platform frames and per-object mesh/material references, plus comparisons against existing isolated platform prefabs. Current overrides make blanket whole-subtree replacement unqualified. Exact aliases require matching mesh GUID/fileID, world matrix, material references and active state. Production cabin aperture ownership remains with the runtime integration lane.
- `receipt.json`: source/geometry/inventory/closure hashes, complete pack file manifest, counts and source stability. Import only `status == PASS` packets with matching hashes.

Object IDs are `unity:<original-scene-guid>:<GameObject-fileID>` and remain stable when the saved scene hash changes. Names and hierarchy paths are descriptive and need not be unique. Matrices contain 16 row-major values and multiply column vectors. Their full values preserve reflections and shear; `lossyScale` is the nonnegative length of each matrix column. The frame is the existing Unity left-handed Y-up world, with an identity source-to-simulation transform. These are saved model units, not independently surveyed dimensions.

`physicalEligible` means active in hierarchy with an enabled, nontrigger saved collider. Inactive objects remain inventoried and are nonphysical. Serialized mesh AABBs or box dimensions provide collider bounds when available; 484 native FBX collider references need isolated Unity bounds verification. No navigation, interaction or material/fire capability follows from a geometry name or renderer. Existing typed source bindings intersect `platform-source.json` with current `semantic-coverage.json`; they grant no capabilities and omit unsupported `door:4`.

The final pack contains 8,048 dependency files (688,973,387 bytes) plus the geometry scene/meta. All source files and metas remain untouched. The raw source scene is intentionally absent from the execution pack, preventing its original behaviour components from running.
