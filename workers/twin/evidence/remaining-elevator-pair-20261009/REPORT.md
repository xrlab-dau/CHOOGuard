# Remaining elevator pair geometry assessment

**No additional pair is supported by the existing geometry.** All six current incomplete landings have two hall leaves, width 1.1m / height 2.1m, Cabin fileID 0, empty CarId, and no original source leaf IDs. None has another authored landing within 0.002m in XZ at a different elevation.

| Landing | Index | Floor Y | Nearest different-floor stop | XZ separation | Floor delta |
|---|---:|---:|---|---:|---:|
| 1Fp-elev-core-w | 1 | 0.0m | B-ELEV2F-wing | 89.137235m | +7.00m |
| 3F-ELEV-corner | 0 | 12.2m | B-ELEV2F-wing | 28.816055m | -5.20m |
| 1Fp-elev-core-e | 1 | 0.0m | B-ELEV2F-wing | 92.412000m | +7.00m |
| 1Fp-elev-core-e | 0 | 0.0m | B-ELEV2F-wing | 89.940611m | +7.00m |
| 1Fp-elev-uminus | 0 | 0.0m | B-ELEV2F-wing | 119.804256m | +7.00m |
| 1Fp-elev-core-w | 0 | 0.0m | B-ELEV2F-wing | 91.736600m | +7.00m |

The two 1F core-w openings are 2.65m apart on the same floor; core-e openings are 2.60m apart on the same floor. They cannot serve as vertical stop pairs. The existing wing pair at Y=7 / 12.2m is the only current pair with cabin references and four hall/car leaves, already bound by ExistingElevatorCarriageSetup.

A read-only scan of the original .blend checked 4m horizontal windows around all six positions, Y intersecting -1..18m, filtering individual meshes wider than 8m in X or Z. Four core landings have zero local records. The upper corner has three signage/structural records; 1Fp-elev-uminus has 23 window/duct/AC/structural records. No local record establishes a cabin floor/walls, distinct car doors and aligned second stop. This bounded scan does not prove that an unnamed cabin cannot exist elsewhere or inside an excluded merged mesh.

Therefore keep these six as unresolved/static hall-door locations. Do not create a cabin, shaft or return endpoint from this evidence. Exact current component IDs, hall pivot IDs, source IDs of nearby archive records, geometry bounds, coordinate registration and hashed evidence are in remaining-elevator-assessment.json and source-near-six-landings.json. No Unity or Assets changes were made.
