# Entire 2F bounded structure handoff

This branch delivered an actual current-source architecture separation proposal, installer/verifier draft and independent mesh preflight. Root owns Unity integration, the canonical registry and final acceptance. This worker made no Unity, Assets, scene or canonical mutations.

The reviewed current scene epoch is `80a55958f086fd89ca524f1124643003a4df387eca05869dea61424650f15af9`. Root's authoritative current inventory contains 3,475 meshes, 3,852 renderers and 2,564 colliders; reused geometry bins were accepted only on exact binary SHA equality. Nine unreadable geometry bins remain explicit omissions.

## Ready structural action

[Native fixed stair separation packet](../../output/floor2-whole-layout-20261009/native-fixed-stair-separation-packet.json), SHA `63c6947fed7c9ab883f74e2366e1d22c06703c84042013ef17ce00cfded08ebc`, partitions the existing `ROOT/g62/g44/g1` fixed stair from one source renderer and its original FBX collider. Its descriptive identity is `native-fixed-stair-ROOT-g62-g44-g1`; no unresolved guide symbol is assigned and no motion is added.

| Current boundary | Exact component | Source faces | Stair faces | Retained faces |
|---|---:|---:|---:|---:|
| MeshRenderer | 829381567 | 97,734 | 1,306 | 96,428 |
| MeshFilter | 829381568 | same renderer mesh | same | same |
| MeshCollider | 829381566 | 101,542 | 1,306 | 100,236 |

The selected geometry corresponds to 83 original source mesh objects, with source IDs and matrices preserved in the packet. Every selected rendered face maps to exactly one current collider face, with identical vertex indices, winding and zero collider geometry error. There is no second exact active renderer or existing motion SourcePartIdentity for this selected stair.

All 94,705 original vertices retain their full position, normal, tangent and UV0 bytes. All 68 submesh/material slots stay in place, including empty selected slots. The selected faces use 2,286 vertices. The child render and collider partition binaries are byte-identical. The parent retains the remaining indices in their original order. Physical and rendered surface unions stay unchanged. Root must recompute bounds from referenced indices because unused source vertices remain in both full channel buffers.

The exact source group ID `93f4f5ec5996ef5390adf44a`, original hierarchy row and hierarchy SHA are sealed separately in [group identity](../../output/floor2-whole-layout-20261009/native-fixed-stair-group-identity.json).

[Preflight](native-stair-separation-preflight.json) passes **239/239** checks. These include actual YAML component types, distinct renderer/filter IDs and their mesh binding; original assets and materials; full source channels; every slot's disjoint index partition; one current physical owner; unchanged original frames; complete output hashes; and exact render/physical child equality. This is offline proposal validation, not Unity installation or runtime validation.

| Proposed mesh | Expected CompleteChannelDigest |
|---|---|
| Render retained | `55c448358052ddfca2565569fe18f51e870018d8fb98e3fd862cf90662270eec` |
| Collider retained | `601fc588cb8b0ff669fd39a48211f06269ad91bf1562a9bfe42278a823209249` |
| Render and collider stair child | `853144f67e6465a3c93fc9f878e8f71147c466ce9b206a9116347affd2cb9da2` |

## Root integration draft

[NativeStairSeparationDraft.cs](../../output/floor2-whole-layout-20261009/NativeStairSeparationDraft.cs) supplies `Apply`, `VerifySaved` and `ReplayOrStrictReject`, each taking no overrides. It pins the reviewed packet, source hierarchy, actual complete source digests, GUIDs, materials and original frame. Initial installation requires the unchanged saved 80a epoch. It creates four cloned mesh assets, one identity-TRS static child and a canonical registry entry; saves the scene; and validates expected dependency digests. The transaction backs up the scene and restores mesh bindings, new assets, child and registry on failure.

The installer draft was not compiled or executed by this worker. Root must review/compile it and independently inspect saved mesh bindings, rendering and physical union. `VerifySaved` logs current and installation epochs while checking exact structural dependencies, so later authorized facade saves can change the overall scene hash.

[Registry draft](../../output/floor2-whole-layout-20261009/native-architectural-parts.registry-draft.json) describes the future canonical `content/world/observed-interiors/native-architectural-parts.json` entry. Root fills actual generated asset GUIDs, file SHAs and saved child component IDs after successful installation. The draft guard accepts unchanged installed dependencies and strictly rejects lost or changed dependencies. Root must invoke that guard at applicable regeneration boundaries; this worker installed no automatic builder hook.

## Other geometry and remaining facilities

[Exact native assembly owner summary](../../output/floor2-whole-layout-20261009/native-assembly-owner-summary.json) establishes ownership of two native assemblies:

- Fixed source stair: 1,306/1,306 source triangles uniquely match one current renderer; maximum source-registration residual 0.000128 model units. The render/collider partition above advances this into a separate static architecture object.
- Source `ROOT/g62/g26/g0/g1/g0` and `g7/g1/g0` escalator pair: 2,624/2,624 triangles uniquely match 82 already-separated operating treads; maximum source-registration residual 0.000120. Retain `esc-well-m-89-0/1` owners and motion; another split or operator would duplicate them.

The older exact assembly receipt also lists a spatial candidate pool. Only its `source_face_correspondence.matches` or the new exact matched-owner summary establishes ownership; unrelated nearby operating parts in that pool are not selected stair faces.

All eleven guide-symbol-to-native assignments remain unaccepted. Their exact inspection results and dispositions are in [whole-layout-handoff.json](../../output/floor2-whole-layout-20261009/whole-layout-handoff.json). The generated Exit9 door operator is exactly rebased at U6.70,V93.98,Y7.03, but its original recipe explicitly infers the 2.6 opening; this does not establish an original native door group or independently verified unlabeled guide-symbol identity. The new photographs do not uniquely identify the other stair/elevator source groups. Frame-like source components are not promoted to shafts.

The 27 unchanged storefront apertures were inspected against current static render/collider geometry using 45 rays each: 1,215 samples. Twenty-six have zero sampled static mesh crossings. The only crossing is three samples at #27's existing COFFEE standee collider, with matching visible board/tube/base geometry. It is not a wall or door and is retained. The current lounge recipe already has a 5.1-unit open entrance with no StationDoor; the new open-entry photographs do not require a gate removal. Four corrected native entry bays 20–23 were excluded from stale recipe replay and have separate root integrated verification.

New source controls are sealed by the source branch in `workers/twin/evidence/floor2-new-sources-20261009/source-controls.json`, SHA `e0cb89d5ce21b9bcd5629b1a58516f017e9477488cd5ea4f005ea887c61edf98`. They directly support the lounge open entry and side glass/rail/down-stair relation, Exit7 flowerstore corridor, and high roof/truss relations. Perspective images do not provide metric wall, shop or ceiling bounds. No additional positively established wall/opening/ceiling pose error was found in this bounded inspection. The 17m source cap stays HOLD; #13/#14/Info directional relationships stay in place.

## Stop and limits

The bounded current-source render/collider stair partition, full channels, exact source identities, installer/verifier draft and independent preflight are delivered. Root decides acceptance and performs installation/replay/Unity validation. All worker writes stay inside its two owned output/evidence directories.

Guide pixels and inspection windows are qualitative controls. No global guide warp, fabricated room/shaft, inferred staircase geometry, ceiling deletion or duplicate operating object is proposed. Eleven guide facility assignments, independent metric dimensions and the nine unreadable bins remain limits. The sampled aperture audit does not certify an entire collision-free or site-identical floor.
