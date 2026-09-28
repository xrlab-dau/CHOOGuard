# SkyPlaza progress

## Milestone: source-aligned plaza and staged specs

### Outputs
- `.planning/2026-09-24-interior-twin/specs/full/make_sky_plaza.py` deterministically generates `sky-plaza.json` (two final-finish Slabs), `sky-plaza-fit.json` (66 non-Slab fitout elements), `sky-plaza-report.json`, two safe-stop routes, and `progress/sky-plaza-overlay.png`.
- `.planning/2026-09-24-interior-twin/kit-folders.json` registers unique `sky_plaza` and `sky_plaza_fit` roots.
- `asset-library/research-public/2026-09-28/SkyPlaza/receipts.json` covers BPA, News1, MOF, KL News, Naver satellite/indoor imagery, OSM and local Hmj/GMl/ssh walkthrough imagery. The 2021 video was downloaded at 720p, sampled into local frames/contact sheets, and deleted.
- The retired `sky-plaza-base.json` concrete-only stage file was removed; its `.validate.json` remains historical evidence. Main built the replacement final-finish Slabs in `geometry-after52`, extending the deck north edge to V188-195.
- Existing `east-exits.json` remains four retained door/reveal elements; the 22 old inferred extension IDs remain retired. Existing EastExits SourceOverrides preview is `pass=true`; no scene/source override writes were performed here.
- KitExtend8 completed `SkyPlaza-1`; its four request fixtures and 29-element combined kit validation report 0 errors. `glassRoof` supports separate Columns, concrete/steelWhite column details and specified ground finishes; the conservative solid Balustrade boundary uses a closed loop.

### Evidence and decisions
- BPA 2017 and News1 2022 support the broad elevated 60 x 100 m plaza/deck; News1 opening photo shows rust-red/brown hardscape, grey pavers and a visible support pier. MOF 2023 confirms the station-terminal public connection.
- Naver satellite broad view (registered with `satuv.py`) supports a broad western lawn bed, narrower eastern planted strip, diagonal path near V185, dark-grey paved region U10-44/V100-130 and the long white roof strip U44-56/V98-190. Bed curb contours remain approximate `[INFERENCE]`, not surveyed.
- Hmj t029/t032 show dark angled steel/yellow accents and warm wood-tone Exit 9 slats; t049/t052 show the long white-framed open-sided walkway with repeated supports and guarded sides. ssh19938 image 14 and GMl t017 agree on the open-sided roof/support form. GMl t170-190 is a different, unregistered dark-underside segment and does not contradict the Exit 10 match.
- The north edge follows the satellite slant V188 west to V195 east. Glass guards cover the exposed edge; a conservative 6.7 m closure replaces the NE end of the north rail at U49-55.7/V≈194-195. OSM way 987862341 and satellite support the NE bridge start; gate hardware/access is `[INFERENCE]`, and no bridge deck beyond the closure is included.
- The ground apron is a plain Y=0 asphalt Slab from V118 to V215 and U-25 to U76, 20 m beyond the deck west/north/east. Owner map `plaza-south-top.png` shows source ground/track geometry through about V117.5; therefore the apron begins north of it. Road/parking/construction-yard use and elevation remain `[INFERENCE]` where satellite is ambiguous; no under-deck buildings are invented. Thirty grounded piers support the deck (25-grid plus five about 2.5 m inboard of the sloping north edge); exact centers remain `[INFERENCE]`.
- `OfficialStation_Layer0_4` is original protected geometry at about U30.9-32.1/V95.9-107.3/Y1.2-7.2. The narrow source fin/support interpretation is `[INFERENCE]`; keep it intact pending Main visual review instead of cutting a likely structural source element. It projects about 0.17 m above deck Y7.03; the slab check reports a contact warning.
- No independent evidence supports a separate sand zone, precise red-brown band/step offsets, an exterior elevator box, or the old 3F wing/bridge continuation. Those remain `still-c`/retired rather than fabricated.

### Validation and next owner
- `sky-plaza.json`: `check_spec.py` 0 errors/1 contact warning; `MajibangBuilder.KitValidate` 0 errors.
- `sky-plaza-fit.json`: `check_spec.py` against live `geometry-after52` reports 66 elements, 0 errors/5 contact warnings; `MajibangBuilder.KitValidate` reports 0 errors. The five warnings are west/east guards and Exit 10 canopy/east guard touching the official station shell at the south frontage; inspect in Main's post-build view.
- Main owns fitout build/save and after renders. Build the 66 elements, run wide voidscan `[-30,90,80,225]`, exercise both updated safe-stop routes and capture before/after renders. Inspect the source fin and all 30 support locations before saving.
