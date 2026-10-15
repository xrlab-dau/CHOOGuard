# Three native paired leaves and generated gate identity

**The three generated gate sets represent the same source bays, but the current native pairs are not ready for operation.** The exact generated specification says these are the outer source vestibule door-leaf openings. Source jambs contain a two-leaf M#001 pair with opposed, mirrored authored 110-degree open poses. This supports bay identity within the existing model, not equality of surfaces or physical site accuracy.

| Generated gate | Original left leaf | Original mirrored partner | Current partner / source triangles |
|---|---|---|---:|
| B-ENH-GATE-doors-outer-2 | 5089b725dad5e3c970223f5a | b1b76f4dc4212fc889f2fb81 | 996 / 1054 |
| B-ENH-GATE-doors-outer-3 | a1f9d8b72e353344d4136264 | 8229d80042f799f775fb53e2 | 1032 / 1054 |
| B-ENH-GATE-doors-outer-1 | bf9da804a36b4cc7dfda9b91 | ebd140c8843a960da0c9681a | 996 / 1054 |

All six have zero current StationDoor.Leaf.Pivot ancestor mappings. The three left leaves have SourcePartIdentity with unconfirmed mapping status; mirrored partners have neither own/ancestor identity nor controller mapping. They are not among the 11 already operated source identities. Exact current object/transform IDs and saved poses are in six-original-leaf-live-bindings.json.

| Gate | Centre offset along native opening | Centre offset normal to native post plane | Frame angle difference | Gate width / native clear width |
|---|---:|---:|---:|---:|
| B-ENH-GATE-doors-outer-2 | 7.899mm | 30.913mm | 0.004704deg | 1.660 / 1.769985m |
| B-ENH-GATE-doors-outer-3 | 2.935mm | 31.338mm | 0.004704deg | 1.640 / 1.769976m |
| B-ENH-GATE-doors-outer-1 | 42.873mm | 30.708mm | 0.004704deg | 1.720 / 1.769985m |

The source posts bound the positive-X opening at approximately X=.050 to 1.820m (clear width1.770m). Paired component origins are X=.078624979 and 1.791360974m (separation1.712735996m). The source header lies at Z=2.10..2.14m. The g6/g7 headers extend across the adjacent negative-X leaf/infill; preserve those shared headers and adjacent objects. Exact source post/header IDs, source matrices, materials and bounds are in three-native-pairs-vs-generated-gates.json.

Two requirements block operation:

- The current partners have only11/14/11 of16 source meshes, missing58/22/58 triangles. The omitted named objects include glass/body geometry on two partners and upper/lower frame fragments. The current roots cannot move complete source leaves. Exact omitted IDs and original material names are recorded. Omission from these roots does not prove absence from legacy combined static meshes.
- Rotating both roots about their component origins into parent-aligned opposed directions yields 87.266/87.273/87.272mm overlap of their free-edge X intervals. This negative fixture rejects that assumed hinge/shut pose; it does not identify a correct mechanical pivot. Preserve observed edit poses until the hinge, shut pose and full closing trajectory are supported.

The supported future work is complete source geometry recovery and reviewed replacement of the approximate generated operator leaves at the same bays. This evidence does not support applying partial partners, assigning a nominal shut pose, removing whole source groups or recommending deletion from overlap alone. No Unity calls, production changes or reconstruction were made.
