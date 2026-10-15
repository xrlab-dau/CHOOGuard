# Two-source regeneration certificate

Both probes are exactly equivalent as orientation-preserving triangle multisets across every supplied corner channel: position XYZ, UV0, normal XYZ and tangent XYZW. No rounding or numeric tolerance was used. Only cyclic corner rotations were normalized; reversed winding was not normalized, and triangle multiplicity was retained.

| Source | Triangles | Referenced vertices | Unmatched triangles | Maximum normal residual | Maximum tangent residual |
|---|---:|---:|---:|---:|---:|
| Kit_Glass | 748 | 1,496 | 0 | 0 | 0 |
| Kit_Trim_Kit_Hairline | 1,548 | 3,096 | 0 | 0 | 0 |

Every source vertex is referenced. Neither source has duplicate position/UV triangle keys or zero-area triangles. Thus no ambiguous duplicate-key pairing affects the zero attribute residual result. No genuine geometry or supplied shading-channel change was found.

The original/generated complete digests differ. These certificates establish triangle-semantic equality of the supplied channels; they do not claim file-byte equality, equality of omitted channels or automatic acceptance by the regeneration guard. Root owns that decision.

`manifest.json` seals both certificate files and exact local snapshots of the input probes. `compare_two_sources.py` reproduces the comparison using the hashed root probe paths. All work stayed in this review folder; no Assets edits or Unity calls occurred. The two-source review is complete and stopped.
