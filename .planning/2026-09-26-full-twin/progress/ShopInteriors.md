# ShopInteriors progress (respawn ShopInteriors3, 2026-09-25)

## Done
- Generator `.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py` (fit fixes 13:50-13:55): open-front keep-out = 1.0 m strip;
  door keep-out w+.5; grid column count by fit; gondola `aisles()` (perpendicular to front or parallel to `aisleEdge`);
  cash counter fallbacks (open kiosk front, side-wall counter); shelf tri model calibrated on KitValidate; budget trim of seats (>=60 %),
  downlight pitch up to 3.4; per-unit overlay tiles.
- Validated 13:54 (0 errors each): shops-1f (112,882 tris), shops-2f-main (25,038), shops-2f-box (39,250), shops-3f (23,042).
  `<spec>.validate.json` next to each. Main messaged per spec (plan edits for replaceBuilder units included).

## Next
- Through-glass renders (poses in shop-work/poses; before renders exist in shop-work/renders/before-*.png) after Main builds.
- Report JSON: add validation/renders/requests sections.
- Report sections validation/requests/receipts added; receipts `asset-library/research-public/2026-09-26/ShopInteriors/receipts.json` (no new downloads).
- 13:58 test render shows 1F-08 still empty -> waiting for Main's build; then render after-<unit>.png with poses/before-<unit>.pose.json
  (1F-02, 1F-03, 1F-08, 1F-11, 2F_box-올리브영, 2F_main-편의점_-43_-32, 2F_main-복순도가, 3F-에그박스).
