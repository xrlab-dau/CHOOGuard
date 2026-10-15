# Unit8 bounded independent review — accepted

The final Unit8 offline rebuild and its root-normalized loader packet pass **382/382** checks. No offline blocker remains. This review is read-only and does not claim a Unity runtime result or field measurement. The guide remains a public schematic reference.

- Raw packet SHA256: `7753b9f595a553712de983c9639bd75b2e9fdaf2f7f52de6ab32cb2881adbb15`
- Root-normalized packet SHA256: `d95a7dc01d1055b40913fbd9a3be6d2366b3b93fcf7b557c2c4c3b43596ee80c`
- Reviewed saved source scene SHA256: `2f780aff3c55d3187419f73c4dd369d8f7f8bee36ad0ecaacc6d193c5cac8264`
- Independent report SHA256: `0d89afa5f849fba9049147b01597dda65c33eb64a96b50a8894e59d798b9bfb2`

The 17 replacements remove exactly 2,014 old faces and append 2,028 faces. All 16 renderer and 2 MeshCollider owners, exact source GUID/file/binary/full channel hashes, materials, and complete matrices match the current export. Old position/normal/tangent/UV0 arrays retain their complete original prefixes; every retained index sequence has an exact hash. No prior moved Unit4/9–16 contribution is removed, and removed face centres do not fall inside any other canonical source-shop interior. Dedicated Unit8 walls and both horizontal finish masks match the source region. Native Ground_Floor, MainShell, ContextPlaza, Room/core and unrelated owners are excluded from all replacements.

Every appended float32 triangle has finite channels, positive normal winding, and passes the actual loader's area/unit-normal/unit-tangent/orthogonality thresholds. The target granite finish at Y.004 and ceiling at Y3 cover the complete 198.976794 model-unit-squared polygon. Existing enabled nontrigger Ground_Floor and ContextPlaza top faces support the target at the established 2e−5 float tolerance; the raw uncovered numerical sliver is about 1.693e−6 square model units. No native support faces are appended. The protected end-core approach has zero overlap.

All seven fixtures remain inside the old/new common region using their actual renderer world-AABB footprints. Five fixture parents translate rigidly; two stay in place. The existing fascia sign and one StationDoor root rotate and translate rigidly. Both original leaf meshes/colliders/local fields and slides remain protected. All 18 downlight discs and 18 annuli retain exact source normals, tangents and UV0, source triangle order, and one rigid translation per fixture.

Door spans are separate contracts: nominal authored opening **1.8**; actual static baked collider gap **1.849999858**; inner post span **1.75**; fully open original leaf BoxCollider gap **1.769999137**. This is an offline geometry result, not runtime capsule clearance.

Root normalization has been checked field by field: source file/count names, empty unchanged-prefix position masks, absolute appended triangle indices, renderer/collider proof separation, seven world rigid motions, explicit local guide path and final source/evidence hashes. There is no second offset of appended indices. Root owns actual Apply, saved scene inspection, leaf motion and capsule/navigation tests.

`packet-initial.json` is superseded; only `packet.json` and `normalized-packet.json` are accepted. The final raw update adds source index-format, selected triangle-index and selected-index-hash receipts; append geometry and motions are unchanged.
