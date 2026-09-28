# 1F suspended-ceiling heights above the local floor (measure-1f-ceilings)

All heights are in u above the **local floor plane** of each reconstruction. The unit is source-model units, the same metre-like unit as the March registration. Thresholds were fixed before any result (`thresholds.json`). A dated addendum was added afterwards. It sets only which gauge supplies the headline number. No threshold and no tier rule changed.

## Method
- **Models used:** March `1f-2026-sfm/sparse/2` (absolute); S20/S22/S23 central-crossval; SY0 model 0 clip K02; kmCzb3KOQ3M rear-row `sparse/2`; eluhiZ_eirE `sparse/0`.
- **Gravity:** taken from plumb lines, i.e. near-vertical LSD segments (vanishing direction). Even and odd frames agree within 0.01–0.26°. The floor normal is within 0.3–0.9° of plumb.
- **SY0 model 1 is bent:** K13–K16 and K18–K20 differ by 22° in gravity, so the model is not rigid.
- **Floor:** one RANSAC plane per rigid model or clip group, with the normal held within 3° of plumb.
- **ρ:** height above that floor divided by the median camera height over the walking frames. ρ needs no gauge.
- **Ceiling planes:** layer detection per cell, then a RANSAC plane with a 0.08 u inlier band and a normal within 5°.
- **Held-out tests:**
  - random 30 % of points (median |residual| ≤ 0.05 u);
  - frame-disjoint: points re-triangulated from even-only and from odd-only observations, planes fitted separately (≤ 0.10 u apart).
- **Gauges:**
  - **Camera prior:** 1.60 u.
  - **Pedestrians:** Vision boxes (balanced mask). The person-height/camera-height ratio does not depend on the floor.
  - **Known ceiling (post-hoc):** the March class-a approach low ceiling (2.306 above the local floor) seen in the same video.
  - **March only:** the absolute registration.
- **Frames:** SY0 frames came from eleven ≤30 s 1080p sections. Frame timing was checked by SIFT reprojection (the correct frame scores best). The downloads have since been deleted.

## Key finding: the camera prior is wrong
- **March camera measured directly:** the absolute registration puts the March walker's camera at **1.40 u** above the floor.
- **Other walkers, via the known ceiling:** SY0 1.35, S20 1.44, S23 1.48, kmCz **1.10** (held low).
- **Pedestrian gauge:** reads 3–7 % high (+4.3 % against the March absolute) but follows the same pattern.
- **1.60 prior:** 14 % high for March and 45 % for kmCz.
  - This is why the provisional rear-corridor value was wrong: that placement put the camera at 1.01 u and fitted fascia-level points.
  - With the 1.60 prior the corridor would read 6.95, which is above the 2F slab underside and not credible.
- **Only S23 passes** the pre-registered 8 % camera-vs-pedestrian gate.
- **Random held-out fails in most places** (0.05–0.11 u) because signs and fascias sit inside the ±0.3 u band.
- **Result: every zone is tier c.**

## Zones

| zone | value | p10–p90 | sources | gauge range | tier |
|---|---|---|---|---|---|
| central approach (GATE3/4 inner header → escalator foot) | 2.31 (March class-a) | 2.25–2.52 | March abs 2.36; S23 2.43 and SY0 2.47 (pedestrian gauge); S20 2.56 and kmCz 3.35 (1.60 prior only) | — | c |
| central lobby (inside GATE3/4) | 4.98 | 4.68–5.66 | March 4.98 abs; S20 4.98, S23 5.12, SY0 4.80 (known ceiling); S22 5.52 (pedestrian, fails the check) | ±0.2 | c |
| rear corridor #9–#16 (#4 corner → U− end hall) | **4.78** | 4.74–4.82 | kmCz 2020-12 only | 4.78 known / 5.37 ped (n=6) / 6.95 prior | c |
| U− lobby / GATE1 hall (#7 end) | **4.81** | 4.65–4.95 | kmCz only; continuous with the corridor | 4.81 / 5.41 / 7.00 | c |
| locker/escalator corridor (escalator foot → U+ hall) | **4.51** | 4.45–4.57 | eluhiZ 2026-09 | ped 4.51 (bias-corrected ≈4.32) / prior 5.31 | c |
| U+ end hall (elevator / KTX특송 / 짐캐리) | **4.41** | 4.26–4.53 | eluhiZ | ped 4.41 (≈4.22) / prior 5.19 | c |
| U+ glazed double-height strip | no ceiling | slab-edge band 5.0–5.6; roof glazing ≈10; structure up to ≈15 | eluhiZ | ped | c |
| U− front corridor #5–#8 | unmeasured | — | no floor-measurable walk | — | — |

**Not measurable:** SY0 clips K03–K20 and the GATE1-hall model have almost no floor points. The floor is polished and the vlog framing is close-up, so they give no second source for the rear corridor, the lockers or the U+ hall.

## Overlays (`overlays/`)
Inlier points are drawn on source frames:
- `central-approach-planes.jpg` — note that the S22 plane landed on a sign.
- `central-lobby-planes.jpg`
- `kmcz-escalator-foot-known-ceiling.jpg` — the kmCz known-ceiling band.
- `kmcz-rear-corridor-ceiling.jpg`
- `kmcz-gate1-hall-ceiling-band.jpg`, `kmcz-gate1-hall-layers.jpg` — the board and exit sign sit below the ceiling.
- `eluhiz-locker-corridor-ceiling.jpg`
- `eluhiz-uplus-hall-regions.jpg`
- `eluhiz-double-height-layers.jpg`
- `sy0-k02-approach-ceiling.jpg`

## Scripts
- `c1lib.py`: floors, plumb, gauges, regions, `measure_plane` with the held-out tests.
- Driver cells ran in the eval kernel against `/tmp/m1c` dumps made with `measure-2f-main/dump_models.py`.
