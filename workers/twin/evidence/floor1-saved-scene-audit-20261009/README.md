# Saved 1F scene preservation audit

**PASS.** Original `before-floor1-guide.unity` and final saved `FpsStation.unity` retain all 26,141 YAML document IDs, types, and ordering. There are 136 changed documents: 69 Transform, 54 MeshFilter, and 13 MeshCollider. Every changed document is attributed to exact declarations in `floor1-packet.json`, `unit16-packet.json`, `unit4-packet.json`, or `unit8-packet.json`. Unexpected chunks and blocking deviations are both **0**.

| Pinned file | SHA-256 |
| --- | --- |
| Original scene | `7d14af92cddc20f6c1de0310a64f9d9261b5cfefff65b60001c6ad1b8f6d608a` |
| Final saved scene | `fa4dc4be6f5bc09c9b6634b7087c687c6dd2950f04ab749fd6ed90c26b463e80` |
| Unit 8 root installer packet | `d95a7dc01d1055b40913fbd9a3be6d2366b3b93fcf7b557c2c4c3b43596ee80c` |

The four packet/installation receipt hashes and all before/after scene chain transitions were verified live. [saved-scene-audit.json](saved-scene-audit.json) contains exact changed document IDs, paths, changed fields, hashes, packet attribution, and complete mesh-reference chains. [audit_scene.py](audit_scene.py) reproduces the result without invoking Unity or writing source/canonical files.

Each changed MF/MC document preserves every byte except its `m_Mesh` line; the final reference exactly matches the declared installed asset. MC attribution is limited to explicit collider owners or a collider on the exact declared MF owner's GameObject with the same stage source mesh. Each moved root resolves uniquely by its declared path or exact StationDoor Element. Root Transform documents preserve every other field, including scales, parent/children links, and Euler hints. World motions match their declarations within float serialization tolerances: maximum position residual 0.000010391 m and quaternion component residual 0.000000055888. This scene contains **0 PrefabInstance documents**; every motion is stored in its exact direct root Transform document.

Native preservation includes all 18,608 FPSWorld documents, all 42 MainShell documents, all 103 StationDoor components, all 1,831 door leaf/child/pivot documents, and all 1,035 SourcePartIdentity components. These sets retain matching original/final aggregate content hashes. The 5 Ground_Floor and 37 ContextPlaza native documents are separately checked in the JSON. All 26,005 other YAML documents are byte-identical; all 6,907 direct Transform scales retain their original values.

Room/ToiletRoom and core surfaces are canonical combined-mesh contributors rather than literal scene roots. This audit proves their scene/component/reference boundaries; face-level preservation for contributors in changed combined meshes belongs to the [independent geometry review](../floor2-architecture-next-20261009/review-floor1-stage/unit8-review/README.md). The scene audit does not infer geometry preservation from an unchanged object name.

Reproduce from the repository root:

```sh
python3 workers/twin/evidence/floor1-saved-scene-audit-20261009/audit_scene.py --final-sha fa4dc4be6f5bc09c9b6634b7087c687c6dd2950f04ab749fd6ed90c26b463e80
```

Only this evidence directory was written by the audit branch. The earlier `pre-unit8-scene-audit.json` records an intermediate validator double-counting issue and is superseded by the sealed PASS above.
