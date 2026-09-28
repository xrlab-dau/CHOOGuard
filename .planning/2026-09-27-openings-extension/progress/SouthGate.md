# SouthGate progress

## State
- 2026-09-25 resumed from the rate-limit interruption. Read resume digest, round plan, previous full-twin plan/baseline, predecessor PlatformSide3 notes and south-concourse request.
- Opening box delivered first to Main: `.planning/2026-09-27-openings-extension/openings/SouthGate.json`.

## Opening evidence and decision
- Prior source evidence: `.planning/2026-09-24-interior-twin/specs/full/platform-side-report.json:261-269` (2F Tracks access at the south end; the protected SSW wall request, previous Naver stair/escalator matches in the separate source south strip; floor top 7.2); `.planning/2026-09-26-full-twin/progress/PlatformSide3.md:11,28-29` (source inner-face equation, continuous glass wall/no door leaf); official 2F plan `asset-library/research-public/2026-09-23/busan-sinmajibang/plans/2026-05-20_kgnwrite-224291656711_004.jpg` shows Tracks directions at the south end.
- Box targets `PlatformsParking/OfficialStation_-부산역_선로상층부_0`, along inner wall u=-13.63-.0722v, V=52..60 (8.0209 m); y 7.21..9.4 preserves both floor plates. The span is within the requested V~50..62 region but exact jambs/width are [INFERENCE], pending RegTech/video registration.
- SourceOverrides.Preview passed with no warnings. Preview report `.planning/2026-09-27-openings-extension/openings/SouthGate.preview.json`: removed 17.5656 m², exactly LxH; 290 wholly removed +117 split triangles; far triangles missing 0; area balance error 0; independent area matches; `checks.pass=true`.
- Paired preview renders: `.planning/2026-09-27-openings-extension/southgate-work/renders/gate-box-side-after.png`, `gate-strip-side-after.png` (both viewed). Poses in corresponding `southgate-work/poses/gate-*.json`.

## Evidence research already in scratch
- `.planning/2026-09-27-openings-extension/southgate-work/` has source geometry and baseline scene render views; `asset-library/research-public/2026-09-27/SouthGate/scan/` has previously downloaded candidate walk videos/frames and contact sheets; `photos/2026-05-30_leeev_-224300854146_002.jpg` verifies only the nearby Weeny Beeny storefront, not gate type.
- Candidate contact sheets show ticket/check gates, station concourse and platform transitions but do not yet geographically resolve which access path is the south-strip gate. No `receipts.json` exists yet; downloaded candidates need URL/date receipts before keeping as evidence.
- Asked RegTech8 to register the candidate clips and return a gate camera/measurement; KitExtend7 confirmed Portal/Ramp usable. Main says no opening is yet applied; waits for opening path to apply/announce audit.

## Next
1. Main applies `openings/SouthGate.json`; record announced geometry-after audit.
2. Continue RegTech/video search to resolve exact position and gate type; update/re-preview box if anchored results shift it. Add evidence receipts.
3. Generator/spec under `.planning/2026-09-24-interior-twin/specs/full/`: `make_south_gate.py` -> `south-gate.json`; use Portal, Ramp and only evidence-backed Gate/DoorSet/signage; run check_spec on announced audit and KitValidate.
4. After Main applies opening and announces audit, run south-strip void scan against that audit; assess each stair/escalator well, guard any unguarded drop and report source defects. Provide report JSON and post-cut renders/receipts.
