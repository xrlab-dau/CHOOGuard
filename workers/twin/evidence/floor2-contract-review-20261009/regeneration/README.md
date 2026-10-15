# Exact 2F main source regeneration diagnosis

All three rejected batches are exact attributed oriented triangle reorder variants. No geometry repair or source-asset rewrite is needed. `sealed-summary.json` lists the three certificates and their SHA-256 values.

| Batch | Vertices | Triangles | Quads swapping corners 1 and 2 | Identity quads |
|---|---:|---:|---:|---:|
| Kit_Frame_Canopy_Dark | 4,128 | 2,064 | 516 | 516 |
| Kit_Glass | 968 | 484 | 121 | 121 |
| Kit_Trim_Kit_Hairline | 1,584 | 792 | 198 | 198 |

Each individual quad retains its exact position, UV0, normal and tangent values. Its triangle indices compensate for the corner permutation, preserving both triangle orientation and multiplicity. The proof permits only cyclic rotation of triangle corners; it does not canonicalize reversed triangles. Both raw JSON numeric equality and original serialized Float32 vertex/index bytes are checked, with no tolerance or rounding.

The raw original probes match the actual serialized source assets byte for byte. Both original and generated `CompleteChannelDigest` values are independently reproduced using the C# binary digest layout. The source batches have exactly Float32 position, normal, tangent and UV0 channels, one triangle submesh, UInt32 indices, and no colors, UV1–7, bone data, bindposes or blend shapes. Generated complete digests match the same channel absence assumptions. `Batch.Fill` uses fresh meshes and adds only these authored channels; the existing runtime guard must continue rejecting additional UV, color, skin and blend channels.

The canonical input is `.planning/2026-09-24-interior-twin/specs/2f-main.json` (19 elements: two suspended ceilings and 17 storefronts). Its recipe and generator hashes are included in the certificates. `KitStorefront` contributes the Frame, Glass and Trim batches. The per-quad identity proof retains contributor order; no triangle is reassigned across another quad or storefront.

Recommend adding only each certificate's exact generated complete digest as a verified variant against its exact original complete digest, baseline source-file hash, frozen probe hash and certificate hash. Preserve all current owner/frame/dependency and omitted-channel gates. Do not accept future source changes by count equality, tolerances, or a generic reorder exemption.

No Unity calls, Scene/Assets writes, canonical registry edits or full production regeneration were performed by this lane.
