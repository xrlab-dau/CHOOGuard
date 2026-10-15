# Bounded Busanok finish review

Final bounded source review is clear. All reported installer findings are fixed and rechecked: Apply freezes retained/reused appearance before Stage and verifies it after SaveAssets; recovery compares MaterialProof for every material, rejecting every dirty loaded property texture. The committed-scene journal-marker and exact production-scene identity fixes remain intact. Promoted setup exactly matches the stable worker and pins approved packet SHA12b0c4c7e2e3898bddf9ec57616e1b90e3b332520be43e1512f174da0a0850f4. No blocking correctness finding remains in this review scope.

## Supported preservation

- Main v2 pure preflight replays actual PolySlab and ShopGeometry families in Main order before any material writes/root deletion, with exact registered source coverage. Five contributors to shared ceiling are retained.
- Only source slab top triangles16/18 are subdivided; original26 other triangles, including bottom/perimeter and other top faces, retain original indices and native vertex prefix. Retained floor38triangles plus wood4triangles partition the source support.
- Independent union/difference checks show tenant rectangle coverage error2.7e-15 model units squared, no packet fragment overlap, and exact Float32 clipped-source union coverage. Tenant area after Float32 conversion90.9999990463 versus intended91.
- Main and existing shadow ceiling use the same retained mesh, removing only Busanok0/1 and retaining other8 triangles and full source channel prefix. Companion extracts the same2 original triangles.
- Source YAML positions/normals/tangents/UV0 profiles are exact Float32; original source binary and asset SHA checks match. Retained originals preserve all8 UV channel declarations/values and existing color channel representation; only new split vertices interpolate attributes barycentrically. Source profile has no colors or additional UV channels.
- Existing wood/gypsum material and mapped texture file bytes/GUIDs match packet; no source photography is used as a texture.
- Floor source renderer/collider share retained partition; wood renderer/collider share complement. Canonical floor entry carries fixed collider companion, while ceiling entry is shared by actual Batch.Save and its shadow generation.

## Transaction and recovery

Inactive staging creates only dedicated companion assets/subtree, then swaps exactly3filters and1collider. Wrapper snapshots all prior refs/worldframes and rejects unrelated changes before save. Original mesh file hashes are frozen before Stage and retained through SaveAssets. Durable pending receipt precedes SaveScene; precommit failure restores refs and removes new objects/assets. Postcommit failure retains journal. Recovery verifies complete meshes, materials/textures, original bytes, frames/colliderflags, exact active saved FpsStation scene and unique scene-scoped owner paths before canonical publishing. Recovering a pre-Save false marker is allowed only when all saved proofs hold.

Apply rejects dirty retained/reused materials and every loaded property texture before Stage, preserves material/texture file bytes and GUIDs through SaveAssets, and checks JToken.DeepEquals before Describe. Recovery now also requires JToken.DeepEquals(MaterialProof(actual material), frozen material proof) in CheckFilter, covering dirty loaded texture rejection before canonical publishes.

## Limits and root validation

No Unity, source/Assets/code changes, staging duplication, network actions or nested delegation occurred in this lane. Root owns compile/runtime tests and actual installation. Request actual fresh Main v2 source regeneration, seam/footprint support raycasts on installed floor, and same-mesh ceiling shadow verification. This proves source preservation and reference appearance scope only; physical site accuracy and full-station completion are unverified. Staging-owner all-scene audit now covers3843 exported active/enabled renderers from the after-glazing snapshot and reports no extra upward floor finish or ceiling plane; six extra coplanar owners contain only unchanged downward structural contact facets. This audit is attributed sibling evidence, separate from this lane's independent packet checks.

Final reviewed file hashes are in final-recheck-snapshot.json; review-snapshot.json preserves the prior findings snapshot. Independent geometry results are in independent-packet-geometry.json. Historical findings remain in findings.md.
