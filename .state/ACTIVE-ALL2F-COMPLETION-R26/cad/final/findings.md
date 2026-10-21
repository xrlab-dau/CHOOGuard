# Findings

- The existing exporter has old author classic:/root/r26_live_cad, hardcoded scope 002 and snapshot-001. Clone to unique final-author C#; never alter frozen prior requests.
- Final author policy is registered for classic:/root/r26_final_cad, own cad/final script root, prefix C180_R26_FINAL_CAD_, maximum timeout 600000 ms.
- Snapshot/output tag 007 is currently unused. Root trigger is still pending, with only Track 9 four-machine writer active.
- Service fronts now include 67 parts and the actual local render/collider aperture in object 1777742185; nursing latest 003 has 298 parts, 9.75 m source-model finish, and 55 fixtures lowered from prior wrong 12.1 m finish mapping.
- Existing builder preserves actual mesh contours/holes using pyclipper nonzero integer union; source vertices remain unchanged. It emits 9 achromatic pages, individual object DXF layers, no visible text.
- Initial snapshot-001 is not current: it predates all R26 saved changes and must not supply final geometry.

- Root clarification: snapshot007 is the actual current editing geometry; dirty was true before and after. SHA09fbe7c5... is the existing saved Scene file hash, not proof that current dirty editing state was persisted or that source bytes are identical. No extra save/reopen/probe.

- Final snapshot007 has 29,588 active renderer records, 3,667 unique meshes and 78 official mesh records. 72 alternative-LOD1 registry IDs are excluded by policy; 40 were active renderers actually encountered. Original LOD0 is used once. Unreadable source meshes: zero.
- Preview.app directory exists but local LaunchServices cannot launch its executable (-10827 kLSNoExecutableErr). This does not alter the completed CAD.
