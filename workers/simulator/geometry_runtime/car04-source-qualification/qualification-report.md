# Car04 original-source qualification

Source: `Assets/ChooGuard/Art/Fps/KTX/ktx-single-10car-left-open.fbx`, SHA256 `1651ec42fc347feb24682cc4ee879227a02110abb4f58f8041988924a82e6a2a`. All offline scans and isolated probes preserve original source bytes.

## Qualified decisions
- **Direct platform-door passage rejected.** The blocker is a real two-level luggage rack, with .883mwidth, .669mdepth, fitted back/returns and source UV imagery. It is not qualified carriage-wall lining. Original rack, outer body, external doors, jambs and floor remain exact.
- **Passenger access through intercar route physically qualified.** Original Coupler03/04 gangway floors support Car03/05→Car04. Only two actual cabin-door leaves are partitioned into reversible isolated copies: each264body/handle+8decal+4glass=276triangles. Original vertex channels and all stationary indices/materials stay exact. .85m leaftranslation is a training approximation, not validated field mechanism.
- **44source seats physically qualified as model geometry.** Eleven rows×four cushions,44/44actual Unity cushion hits, maximum .000590501merror; source backrest correspondence and source floor .448m retained. Rows6/8 face reverse. Existing40heuristic candidate contains12anchors over .3m from actual seats, max .844m; do not activate it. No surveyed seat labels or current field count claim.

## Actual isolated Unity evidence
V1PID1240 exit4 in33.219s;V2PID1717 exit4 in21.231s;V3PID2574 exit4 in21.47s. Failure is native NavMesh attachment, not physical passage.
- Open and restored routes from both neighboring vestibules: real .28mradius/1.72mheight CharacterController reaches Car04 target, .134/.143m endpoint errors.
- Closed cabin doors: both capsules refused,5.91/7.18mshort.
-112/112originalsource floor ray probes pass,44/44source cushions pass.
- Exact3mesh partition, all original vertexchannels/materialrefs restored, zero owned objects remain; external4door remains blocked.
- Neighbor starts attach to native NavMesh within .0183m, but Car04 cabin/farvestibule has no NavMesh within1m.
- V3 inspects actual sourcefloor winding: upward cross normals and positive transforms. Additional32nav-only sourcefloor triangles,0positionerror,0orientationflips, all original obstacles retained, do not restore attachment at .06voxel. The missing-floor/winding hypothesis is contradicted.

## Actual native path qualification
V4PID3459 PASS0 in20.484s. Root-approved local NavMeshData uses all original train colliders in12×4.5×24m bounds, voxel.02m, agentradius.28m/height1.72m/climb.28m. No floor triangles or physicalcolliders added. All original sourceobstacles retained; globalvoxel.06 unchanged. Bothneighbor3/5→Car04 open/restored paths are PathComplete and realcapsulesPASS. Bothclosedpaths are PathPartial and capsulesrefuse. Bothsourcegangwaylinks qualify; targetNavMesh error .00582m.44cushions/112sourcefloorprobes remainPASS;originalrefsrestored0ownedobjects.

## Production handoff boundary
Root reviews `passenger-semantic-proposal.json` before production class/manifest integration. Original external door4 remains blocked; no door:4 created.44modelseats and two cabin-doorIDs may be activated only through accepted runtime adaptation. Exactreverse trips through each individual gangway remain a useful production-harness check; current actual capsule probes enter4fromboth3and5. No production manifest/Core/seat/door activation has occurred.

## Artifacts
- `source-leaf-contract.json`: exactleaf source bounds and triangle identities.
- `source-intercar-open-route-packet.json`:0sourcefloor gaps,1.65million offline sphere samples, conservative .28214/.28326m clearances.
- `source-seat-correspondence-packet.json`:44cushion/back/floor coordinates, reverse facings and old40comparison.
- `source-rack-from-aisle.png`, `source-cabin-door-from-vestibule.png`, `source-seats-overhead.png`: exactsourcegeometry/UV diagnostic renders, not Unity graphics acceptance.
- `unity-source-intercar-v*.json`: retained actual probe receipts.

## Production implementation closure
Rootextendedscope andreviewedproductionadapter. FinaldevelopmentV3PID8021PASS0 in51.756s, fullPlayMode andcanonicalcar04_passenger_accessPASS. Bothforward+reversephysicalroutes,2logicalcabin-door coreactions,44sourceapproachpaths,actualNPC10.818marrival/corefeedback and20tickreplayPASS. Original7externaldoorchecksPASS:closing3/5allowslegitimateintercaralternates thatavoidcapsule-expandedclosedaperture;directcapsulesrefuse. `production-final-freeze-receipt.json` records exact5ownedsourcehashes, actualnativehashes, original352assetequalityandreportedmainScenedrift andownedprocesscleanup. Rootownsfinalall-codeSourceManifeststage/fingerprint andstandalonegraphics proof.

Finalguardcorrection: original352assetsunchanged, but FpsStation.unitySHAchanged fromlane-start4f25d18d. Nooriginalscene/Assets/sharedUnitywritesorcallsissuedbythislane;currentfilepreserved,rootnotified. Seeproduction-final-freeze-receipt.json.source_scene_driftforhashes/mtime.
