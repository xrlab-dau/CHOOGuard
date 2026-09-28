# EastExits lane progress (openings 2, 3, 4 + east world extension)
Work dir: `.planning/2026-09-27-openings-extension/east-exits-work/`; evidence `asset-library/research-public/2026-09-27/EastExits/`.

## Evidence so far (21:30)
- Naver 2F/3F indoor tiles (harvest r/, z20) resampled to UV: `east-exits-work/navmosaic.py` -> nav-2f-east.png, nav-3f-east.png,
  nav-1f-east.png, nav-2f-wing.png. 2F/3F building outline shows the wing: west edge (2.3,91)->(-0.5,128), east edge
  (11,96)->(9,128) (aligned with the box frame, rot 4.13), corridor band U ~4.5-8 through the facade to V ~126, label
  "나가는곳 8번출구" (Naver; blog map + photos say exit 9 -> Naver label outdated), elevator POI 2F+3F (1.3,120.5).
  1F outline of the wing starts at V ~106 (ground-level structure east of the tracks).
- OSM way 165346389 wing U 3.6-14.5 V 98-125.5 (OSM ~+4 U vs Naver); footway bridge 987862341 from (8.8,125) -> (4.5,192) -> (105,198) ...
- Blog simplyssol 2023-12-30 (https://m.blog.naver.com/simplyssol/223307683182) photos in simplyssol/: 03 exit 9 = "3층 올라가는
  에스컬레이터 아래"; 04 exit-9 door bank + yellow sign "9 부산항대교 전망 / 부산항국제여객터미널 / 충장로(부두)방면 / 나가는 곳";
  05 exit 10 corridor between HOLLYS COFFEE and Krispy Kreme; 06 exit-10 doors, sign "10 부산항대교전망 / 충장로(부두)방면 /
  부산항국제여객터미널", behind: glazed covered passage; 07 부산항 하늘광장 (open deck plaza, white railing); 08 covered deck.
- 보행데크: 부산역~환승센터~국제여객터미널 950 m, width 26-60 m (2030busan blog 2017, nocutnews 4898516).

## Next
- read more blogs (ssh19938 223027519848, dxsuckit69 222977105176, kwk776 1303), YouTube walk (6_ifj_u8aio) -> frames.
- model geometry at the facade (audit after33), exit 10 position, deck extents.
