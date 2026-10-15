# Final 1F stage review

The sealed 48-mesh packet passes all **546 independent offline checks**: 510 ownership/matrix/mask/motion checks and 36 BOH topology/channel/generic-collider checks. Packet SHA: `86567626685e262bcbefa72f8eeb6cdeec3d16b748cd1d804f64c0464b7e5f3a`. It contains 46 renderer owners, 17,820 selected vertices and 51 motions: 44 fixture groups with renderer/collider coverage and seven exact baked `StationDoor.Element` identities.

The initial packet omitted seven signs, frontage bulkhead skins/joints and fascia caps, and split 36 coincident fascia vertex groups. Root corrected the buffer, explicit sign ownership and position welding. The final packet has zero split groups, zero wrong per-unit fascia translations and zero omitted exact unit09–15 frontage finishes. The small unit14 party-edge correction reproduces its declared target geometry; all other unit translations remain rigid.

The collider sweep produced two bounded additions: 80 fixed storefront-glazing triangles/160 vertices at the seven old frontlines, and 24 triangles/48 vertices for unit9's cooler and unit12's bakery shelf. These are separate from the 44 moving model groups. Both staged mappings equal the source-byte-matched candidates exactly. Candidate evidence is frozen at SHA `42428651a58412720e499b11b43f71b8c58895942e19d888d2ea7bf16a5c664c`, using `owners: []` and explicit `collider_owners`.

The BOH patch removes only the final segment's 16 joint and 82 clad triangles. Its 24 reused vertices are exclusive to removed faces; all retained face positions and indices remain exact source values. Twelve added triangles form a closed eight-corner box with outward winding, unit orthogonal normals/tangents and UV edge lengths matching the new plain profile. No old 7.75-unit profile is compressed. The eight-vertex public-collider update preserves its preceding endpoint and all other collider faces.

The photographed board explicitly labels **1F** and the open **전문식당가** between rear9–15 and the opposing front3–7 strip. Native nearest retained-wall distances after the proposal are 13.39, 13.08, 12.82, 12.90, 14.05, 10.57 and 3.80 model units. These are geometric distances, not usable aisle widths or field measurements. An exact public-collider section at Y1.5 found only the old BOH faces12/14 entering proposed unit9; after the BOH update, no generic public collider section enters the seven inset footprints.

Root's saved installation receipt and actual scene file hashes match the sealed packet: before `7d14af92…`, after `01df70aa287b34116d452c5f100f1ec5cca93c925973898c1a502dd570534cf0`. This lane made no production/Unity writes. Root owns Unity runtime, navigation, rendering and the remaining guide/front-layout crosscheck.

Read `integrity-review.json`, `boh-topology-review.json`, `aisle-review.json` and `generic-collider-candidates.json` for exact owners, matrices, triangle ordinals, masks and hashes. The bounded review is complete and stopped.
