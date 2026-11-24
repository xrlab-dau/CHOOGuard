# Station fire tutorial equipment

`extinguisher-detailed.fbx` derives from the existing `Assets/ChooGuard/Art/FireSafety/korean_fire_extinguisher_01_2k.fbx`.

- Publisher: Poly Haven; creator: UM JOORIN.
- Source: https://polyhaven.com/a/korean_fire_extinguisher_01 ; license: CC0.
- Original Git blob: `7b9f43aa061f15fa60cdbc97bad39653ee021499`.
- Original file SHA-256: `21d92267a43e1b2493d88a983b36c72a9d31088c9bbe1132ea8c2bafb2836f5e`.
- Derivative: source connected components partitioned into Body, SafetyPin, Lever, StowedHose, Nozzle and Base with physical pivots/anchors. Original geometry and UV partitions are retained; the original asset and its GUID/materials are untouched.
- New derivative materials are plain PBR colors sampled from source parts and do not sample image-texture lettering. Unconfirmed manufacturer/specification/inspection/gauge text is omitted.
- The visible source `소화기` marking on the base is traced from the original source paint into seven closed physical contour meshes, preserving four counter loops. No font/TMP/decal/text image substitutes.
- Pin/lever travel, nozzle grip poses, hose curve and performance are game tuning, not manufacturer specifications or proof of KORAIL field equipment.
- Source-part export roundtrip geometry was checked outside Unity. Unity import, actual contact/aim and visual quality require separate runtime verification.

`DetailedRadio.prefab` reuses existing beveled mesh/material assets from `Assets/ChooGuard/Art/Tutorial/Radio-*`. The lesson fits a broad orange front PTT from that same mesh system; only the lesson instance is changed. A waist dock is separate from explicit carry/stow. No original radio assets are changed.

The two belt stanchions reuse `Assets/ChooGuard/Art/Emergency/Materials/Stanchion.asset`, `CordonPost.mat` and `CordonHead.mat` unchanged. The source mesh is 0.934 MODEL metres tall with a 0.33 MODEL metre base diameter. Posts remain separate, movable objects; the retractable belt end/receiver and solid red strap are lesson-authored parts. Dimensions and placement are not surveyed station inventory. Existing station geometry, floor openings and source assets remain authoritative.

The first review metal bin and second review carton fire have been retired after user feedback. The current fire is placed on the actual existing wood-lamination public waiting bench. Only lesson-owned flames/smoke and a local char/powder overlay using shared source mesh references are added. Original bench meshes, transforms, materials and source scene are unchanged. The overlay shader is authored for this lesson; it is visual state feedback, not a combustion simulation. The nearby red extinguisher stand and coaching floor guides are authored lesson props, not surveyed inventory or permanent station markings.
