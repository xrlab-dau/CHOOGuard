# Resume digest for Services23F-2 (the previous agent was killed by a session interruption at 13:40:26, 2026-09-25)

Full previous transcript (grep it for details, e.g. measurements, URLs, frame times): ~/.omp/agent/sessions/-CHOOGuard/2026-09-22T09-47-00-085Z_01a0c882-f275-7000-a4e8-29539b53d97a/Services23F-2.jsonl

## Files it wrote or edited
- agent://Public1F-2

## Messages it received
- [Wait interrupted by message] <irc from="parent" agent="Main"> Main: agent ids in this round are SeamPassage-2, PlatformSide-2, Public1F-2, Services23F-2, ShopInteriors, RegTech-2, KitExtend (write agent://<id>). check_spec.py already knows the plan.md contract types Room, ToiletRoom, Window, Canopy (Room/ToiletRoom walls are checked as mounted walls with openings left out; Window/Canopy as mounted). Main is rebuilding the majibang builder root shortly (GATE2 atrium guard fix) — renders taken in the next few minutes may show that change. </irc>
- [Wait interrupted by message] <irc from="parent" agent="Main"> Main: new contract kit type `Slab` (plan.md "New kit types"): walkable floor plate with collider — `polygon`, `holes?`, `y` = top | `depth` .3, `finish`, `edgeFinish`, `soffit`, `collider` true. Use it for any new walkable floor (passages, decks, landings); FloorFinish stays a collider-less overlay. check_spec.py knows Slab. Also: `.planning/2026-09-26-full-twin/voidscan2/voidscan2.json` + `voidscan2-{0,7,12}.png` is a collider-based scan (player-reachable floor, fall edges, doors/glass with nothing behind) — use its runs in your area as a checklist; Main is fixing the thin gap strips between the 2F cell floor and the facades its
- [Wait interrupted by message] <irc from="parent" agent="Main"> Main: candidate location for the 2F U+ end rooms (official 2F guide: 기차 타는 길 →, 역무실·유실물, 수유방, EV, W.C at that end): the main-building 2F cell floor ends at U 67.6 (V -6..12) with no wall and a 58.8 u² void beyond it to the east facade (U 67.5..77.25, V -6.75..13; voidscan2 fall-7-31, .planning/2026-09-26-full-twin/voidscan2/views/fall-7-31.png). If evidence places rooms there, enclose them (Room/ToiletRoom + a Slab floor where needed); SeamPassage-2 owns the 기차 타는 길 passage — agree the split with it (write agent://SeamPassage-2). </irc>

## Its last reasoning (newest last, truncated)
- There aren't clear door symbols in the plan, so I'm trying to figure out restroom entry points from the layout itself—the notch in the upper block might indicate where the corridor connects to the public access area, but without photos or a guide image this is hard to confirm from the raster alone.  Given the scope, I need a systematic pipeline: segment the corrected rasters by color to isolate lavender toilets versus grey rooms/shops, extract contours and polygons per region, then match each polygon against shifted Naver POI points to classify by name and category. From there I can decide what to actually build — toilets by gender type, meeting rooms as interior offices, nursing rooms, and station offices — prioritizing the shells with strongest evidence.  I also need to reconcile these new kit rooms against the live scene's existing builder units, since several service rooms (toilets, station office, nursing room) already exist as placeholder boxes at slightly wrong positions. That means any accepted room in these categories will require a "replaces" relationship plus a builder plan edit to remove the old unit boxes before placing the corrected geometry.
- After the correction, the box's west edge lines up with the live layout's west wall. I'm placing a lavender toilet room within the rotated box, and there's an icon nearby that seems associated with a larger grey room, possibly back-of-house or the toilet block itself.  The icon at the corner is actually a public toilet POI, confirming the lavender room is one toilet, adjacent to another labeled space. Comparing against the board, I see two toilet rooms noted -- one near the entrance at the top-right and another next to the elevator -- which matches up with the north and south rooms I've identified in UV coordinates, though the large grey room spanning that area might actually represent multiple distinct rooms rather than one.  Let me check the existing evidence files for toilet-related entries, searching for terms like "화장실", "toilet", or "restroom" to see if any frames confirm the entrance locations in that box.
- Given the scope of this task, I'm mapping out the second floor deliverables, starting with the west wing layout — restrooms, meeting rooms 201/202, the nursing room, and the elevator and stairwell positions.  I'm also flagging areas that fall outside my scope, like the west building stairs, and cataloging the unlabeled staff rooms. Moving to the box structure, I need to note the office room, nursing room, ticket machines, restrooms, and the ATM.  For the east end, I'm tracking the restrooms along the angled facade and figuring out what's in the void space near the main hall. I still need to place both ATMs correctly and think through how to handle the double-height hall ceiling measurement, which might require image registration I can't easily do.  Shifting to the third floor, I'm noting the restroom locations that need raster correction, plus the food court, which will be tricky since it needs footage registration I may not be able to complete.  Now I'm cross-referencing these against the plan builder's placeholder boxes for restrooms, office, and nursing room to figure out what needs replacing. Next I want to pull up the corrected east overlay and the 3F raster to verify everythi
- I'm checking whether the corrected Naver east facade purple line matches up with the black reference line, but I'm noticing a discrepancy where the green floor area seems to extend past the facade boundary that Main specifies, so I need to reconcile these coordinate ranges.  Looking more closely, that lighter grey area near u 67.5-72 is actually a Naver grey room overlapping the space, and the void at level 7 might have a slab underside I hadn't accounted for. The east facade toilet block appears to be a rotated strip of rooms running along the facade at roughly -65°, containing a sequence of grey rooms and men's/women's restrooms, plus a lighter grey area that could be a corridor or open void, along with stairs, an elevator, and a sign labeled for the train platform.  I'm second-guessing which end of the guide is which — I think the right side of the plan actually corresponds to -u rather than +u, since the Krispy Kreme exit near the top-left aligns with +u,+v. That would mean the "기차 타는 길 →" arrow on the right points toward the west platform area around u -35 to -20, matching up with the tracks deck location.  Checking Main's candidate void region (u 67.6-77, v -6..13) against th
- I'm noting that the 1F elevator aligns with the 2F elevator shaft near the east end, with 2F stairs nearby, so this void region actually covers the elevator/stair circulation between floors — likely belonging to the public/seam passage domain rather than a standalone room.

## Every step it took (tool: intent | command head)
1. read: Reading plan | .planning/2026-09-26-full-twin/plan.md
2. read: Reading kit spec | .planning/2026-09-24-interior-twin/kit-spec.md
3. bash: Listing scratch and outputs | ls .planning/2026-09-26-full-twin/ .planning/2026-09-26-full-twin/* .planning/2026-09-24-interior-twin/specs/full/ asset-library/research-pu
4. bash: Inspecting verification.json | cd .planning/2026-09-25-interior-enhance && python3 -c " import json;d=json.load(open('verification.json'));print(d.keys()) for k in d:   v=
5. read: Reading job output | proc://bg_3
6. bash: Reading stillC | cd .planning/2026-09-25-interior-enhance && python3 -c " import json;d=json.load(open('verification.json'));print(json.dumps(d['stillC'],ens
7. bash: Reading 2F main and 3F reports | cd .planning/2026-09-24-interior-twin/specs/enhance && ls && python3 -c " import json for f in ['2f-main-report.json','3f-report.json']:   d
8. bash: Reading 3F gaps | cd .planning/2026-09-24-interior-twin/specs/enhance && python3 -c " import json d=json.load(open('3f-report.json')) for g in d['gaps']:   if
9. bash: Inspecting majibang plan units | cd .planning/2026-09-23-video-twin && python3 -c " import json d=json.load(open('majibang-plan.json')) print(list(d.keys())) for u in d.get(
10. grep: Finding service unit build code | AgentScripts/MajibangBuilder.cs
11. read: Reading ServiceRoom | AgentScripts/MajibangBuilder.cs:1490-1530
12. read: Viewing official 2F guide | asset-library/research-public/2026-09-22/busan-station-interior/floorplans/naver_shoobong_2F_안내도.jpg
13. read: Viewing registered 2F board plan | .planning/2026-09-23-video-twin/sinmajibang/board2f-plan.png
14. read: Viewing void scan 2F | .planning/2026-09-26-full-twin/voidscan/voidscan-7.png
15. bash: Inspecting voidscan json | cd .planning/2026-09-26-full-twin/voidscan && python3 -c " import json;d=json.load(open('voidscan.json'));print(list(d.keys()));print(json.d
16. bash: Finding Naver indoor data | cd asset-library/research-public && ls; ls 2026-09-2*/ | head -80; find . -iname '*indoor*' -o -iname '*sisul*' -o -iname '*gnd*' | head -40
17. bash: Listing naver indoor directory | cd asset-library/research-public/2026-09-22/busan-station-interior && ls; ls naver-indoor | grep -v pbf; ls naver-indoor | wc -l
18. bash: Inspecting naver indoor geojson | cd asset-library/research-public/2026-09-22/busan-station-interior/naver-indoor && python3 -c " import json,collections d=json.load(open('bu
19. bash: Counting floors in Naver data | cd asset-library/research-public/2026-09-22/busan-station-interior/naver-indoor && python3 -c " import json,collections d=json.load(open('bu
20. bash: Checking floors metadata | cd asset-library/research-public/2026-09-22/busan-station-interior/naver-indoor && python3 -c " import json,collections d=json.load(open('bu
21. grep: Finding Naver tile fetch scripts | .
22. read: Reading Naver acquisition notes | .planning/2026-09-22-station-interior-build/browser-acquisition.json:25-80
23. bash: Checking zorders | ls .tools/mvt/ ; cd asset-library/research-public/2026-09-22/busan-station-interior/naver-indoor && python3 -c " import json,collections d=j
24. write: Asking Public1F about Naver floors | agent://Public1F-2
25. bash: Probing Naver tile floor params | cd /tmp && B="https://map.pstatic.net/nvbpc/indoor/indoor/656260fa-d2fd-4959-980d-9376762761ca/getTile/225036/103731/18/pbf"; for q in "" "?
26. bash: Checking MVT decoder API | cd /tmp && sed -n 1,40p /Users/um-yunsang/CHOOGuard/.tools/mvt/decode_mvt.py; python3 -c " import sys;sys.path.insert(0,'/Users/um-yunsang/C
27. bash: Decoding sample tile | cd /tmp && python3 -c " import sys,collections;sys.path.insert(0,'/Users/um-yunsang/CHOOGuard/.tools/mvt') import decode_mvt as m t=m.decode
28. bash: Decoding sample tile layers | cd /tmp && python3 -c " import sys,collections;sys.path.insert(0,'/Users/um-yunsang/CHOOGuard/.tools/mvt') import decode_mvt as m t=m.decode
29. read: Reading decode output | proc://bg_7
30. write: Coordinating with Public1F | agent://Public1F-2
31. read: Reading browser docs | xd://eval/browser
32. eval:  | const tab = await browser.open({ name: "nv", url: "https://map.naver.com/p?c=19.00,0,0,0,dh&lng=129.0405&lat=35.1155", viewport:{width:1400,
33. eval:  | const lon=129.0399028, lat=35.1157262; const x=lon*20037508.34/180, y=Math.log(Math.tan((90+lat)*Math.PI/360))*6378137; const tab = browser.
34. eval:  | const lon=129.0415, lat=35.1152; const x=lon*20037508.34/180, y=Math.log(Math.tan((90+lat)*Math.PI/360))*6378137; const tab = browser.tab("n
35. eval:  | const tab = browser.tab("nv"); const lon=129.0412, lat=35.1156; const x=lon*20037508.34/180, y=Math.log(Math.tan((90+lat)*Math.PI/360))*6378
36. eval:  | const tab = browser.tab("nv"); const reqs = await tab.requests(); const u = [...new Set(reqs.map(r=>r.url.replace(/getTile\/.*$/,'getTile/*'
37. bash: Fetching new dataset tiles | cd /tmp && for t in 450073/207463/19 450074/207463/19 450075/207463/19; do curl -s -m 20 -H 'Referer: https://map.naver.com/' -o nt.pbf "htt
38. eval:  | const tab = browser.tab("nv"); const reqs = await tab.requests(); const js = [...new Set(reqs.map(r=>r.url).filter(u=>/\.js(\?|$)/.test(u)))
39. eval:  | const tab = browser.tab("nv"); const srcs = await tab.evaluate("[...document.scripts].map(s=>s.src).filter(Boolean)"); display(srcs); const 
40. eval:  | const t = await (await fetch("https://ssl.pstatic.net/sstatic.map/openapi/maps3-gl.js?ts=1789637336569")).text(); globalThis.glsrc=t; const 
41. eval:  | const t=globalThis.glsrc; const out=[]; for (const k of ['getIndoorGroundTileUrl','setIndoorGroundObject(e){','INDOOR_GROUND_TILE].source','
42. eval:  | const tab = browser.tab("nv"); const r = await tab.run(async ({page})=> page.evaluate(()=>{   // find mapbox instances   const out=[];   for
43. bash: Trying zone+zorder params | cd /tmp && B="https://map.pstatic.net/nvbpc/indoor/indoor/4b03767b-3802-4c62-82f7-5cd1a128cbeb/getTile/450075/207463/19/pbf"; for q in "?zon
44. bash: Fetching indoor metadata | cd /tmp && curl -s -m 30 -H 'Referer: https://map.naver.com/' -o meta.json -w "%{http_code} %{size_download}\n" "https://map.pstatic.net/nvb
45. bash: Testing raster indoor floors | cd /tmp && for zo in 300000 301000 302000; do curl -s -m 20 -H 'Referer: https://map.naver.com/' -o r_$zo.png -w "%{http_code} %{size_downlo
46. read: Reading raster test | proc://bg_10
47. bash: Testing vector style tiles | cd /tmp && for e in pbf mvt; do curl -s -m 20 -H 'Referer: https://map.naver.com/' -o v.$e -w "%{http_code} %{size_download} %{content_type}
48. read: Viewing floor raster tiles | /tmp/floors.png
49. eval:  | const t=globalThis.glsrc; const out=[]; for (const re of [/[`'"][^`'"]*\{zorder\}[^`'"]*[`'"]/g, /[`'"][^`'"]*zorder=[^`'"]*[`'"]/g, /[`'"][
50. eval:  | const t=globalThis.glsrc; const out=[]; for (const k of ['TILE_PICKER)','getIndoorGroundQuery()}','format:`']) { let i=t.indexOf(k); let n=0
51. eval:  | const t=globalThis.glsrc; const out=[]; for (const k of ['TILE_PICKER','getIndoorGroundQuery(']) { let i=t.indexOf(k); let n=0; while(i>=0 &
52. eval:  | const t=globalThis.glsrc; const out=[]; for (const k of ['INDOOR_MAP_TYPE=','FILE_FORMAT=','CRS_TYPE=']) { let i=t.indexOf(k); if(i>=0) out.
53. bash: Fetching indoor picker json | cd /tmp && for zo in 300000 301000; do curl -s -m 20 -H 'Referer: https://map.naver.com/' -o p_$zo.json -w "%{http_code} %{size_download}\n"
54. bash: Harvesting picker POIs all floors | mkdir -p /tmp/nv && cd /tmp/nv && python3 - <<'EOF' import math, json, subprocess, concurrent.futures as cf, os def tile(lon,lat,z):     n=2
55. bash: Inspecting unnamed POI props | cd /tmp/nv && python3 -c " import json for o in json.load(open('picker_all.json')):   if o['zorder']!=300000 and not o['name']: print(o['zor
56. bash: Resolving place id via summary API | cd /tmp/nv && for u in "https://map.naver.com/p/api/place/summary/2140605998" "https://map.naver.com/v5/api/sites/summary/2140605998?lang=ko
57. eval:  | const tab = browser.tab("nv"); const r = await tab.run(async ({page})=> page.evaluate(async()=>{   const out={};   for (const u of ['/p/api/
58. TEXT Place summary works in-page. Resolve all 2F/3F ids (and 1F for reference).
59. eval:  | const all = JSON.parse(await Bun.file('/tmp/nv/picker_all.json').text()); const ids=[...new Set(all.map(o=>o.id))]; const tab = browser.tab(
60. bash: Harvesting picker z20/21 | cd /tmp/nv && python3 - <<'EOF' import math, json, subprocess, concurrent.futures as cf, os def tile(lon,lat,z):     n=2**z; x=int((lon+180)
61. write: Sharing Naver endpoint findings | agent://Public1F-2
62. read: Reading z20 harvest output | proc://bg_16
63. bash: Reading Public1F transform | cd .planning/2026-09-24-interior-twin/specs/full/public1f && ls && python3 -c " import json;d=json.load(open('naver-1f-uv.json'));print(json
64. bash: Creating UV transform helper | mkdir -p .planning/2026-09-24-interior-twin/specs/full/services23f asset-library/research-public/2026-09-26/Services23F/naver-floors && cat 
65. bash: Mapping 2F/3F POIs to UV | cd .planning/2026-09-24-interior-twin/specs/full/services23f && python3 -c " import json,nvuv S=json.load(open('/tmp/nv/summaries.json')) fo
66. bash: Downloading per-floor raster tiles | cd /tmp/nv && python3 - <<'EOF' import math, subprocess, concurrent.futures as cf, os def tile(lon,lat,z):     n=2**z; x=int((lon+180)/360*n
67. TEXT Resolve new ids.
68. eval:  | const all = JSON.parse(await Bun.file('/tmp/nv/picker_all.json').text()); const S = JSON.parse(await Bun.file('/tmp/nv/summaries.json').text
69. bash: Adding inverse transform | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat >> nvuv.py <<'EOF'   def uv_to_ll(u, v):     m, t = T["matrix"], T["tran
70. read: Reading inverse test | proc://bg_19
71. bash: Resampling 2F raster into UV | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > resample.py <<'EOF' """Resample the Naver per-floor raster tiles (z20)
72. read: Viewing 2F UV raster | /tmp/nv/uv2f_half.png
73. bash: Inspecting live geometry and check_spec | ls .planning/2026-09-23-video-twin/1f-implementation/geometry-after14 | head; grep -n "class Live\|def \|^def " .planning/2026-09-24-interio
74. read: Reading check_spec Live | .planning/2026-09-24-interior-twin/specs/enhance/check_spec.py:1-160
75. bash: Slicing live geometry | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > slice_live.py <<'EOF' """Horizontal slices of the live audit (geometry
76. bash: Rendering west 2F overlay | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > overlay.py <<'EOF' """Overlay: Naver per-floor raster (UV-resampled), 
77. bash: Finding python with matplotlib | cd .planning/2026-09-24-interior-twin/specs/full/services23f && for p in python3 /opt/homebrew/bin/python3 ../../../../../.tools/regtech/ven
78. bash: Rendering west 2F overlay | cd .planning/2026-09-24-interior-twin/specs/full/services23f && PY=../../../../../.tools/regtech/venv/bin/python; $PY overlay.py 301000 8.2 
79. read: Viewing west 2F overlay | /tmp/nv/ov-2f-west.png
80. bash: Debugging overlay run | cd .planning/2026-09-24-interior-twin/specs/full/services23f && PY=../../../../../.tools/regtech/venv/bin/python; $PY overlay.py 301000 8.2 
81. bash: Debugging overlay | cd .planning/2026-09-24-interior-twin/specs/full/services23f && PY=../../../../../.tools/regtech/venv/bin/python; $PY -c " import sys; sys.a
82. TEXT The file exists (read tool timing). View it.
83. read: Viewing west 2F overlay | /tmp/nv/ov-2f-west.png
84. bash: Dumping live floor grids | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > floorgrid.py <<'EOF' """Dump live walkable-floor grids (check_spec.Liv
85. bash: Adding floor mask to overlay | cd .planning/2026-09-24-interior-twin/specs/full/services23f && /opt/homebrew/bin/python3 - <<'EOF' import re s=open('overlay.py').read() s=
86. read: Viewing west 2F overlay v2 | /tmp/nv/ov-2f-west.png
87. bash: Rendering box-west and east overlays | cd .planning/2026-09-24-interior-twin/specs/full/services23f && PY=../../../../../.tools/regtech/venv/bin/python; $PY overlay.py 301000 8.2 
88. read: Viewing box-west overlay | /tmp/nv/ov-2f-boxw.png
89. bash: Checking overlay files | sleep 2; ls -la /tmp/nv/ov-*
90. read: Viewing box-west overlay | /tmp/nv/ov-2f-boxw.png
91. bash: Cropping raster with grid | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > crop.py <<'EOF' import sys from PIL import Image, ImageDraw im = Image
92. read: Viewing raster crop box-west | /tmp/nv/c2f-boxw.png
93. read: Viewing east overlay | /tmp/nv/ov-2f-east.png
94. bash: Resampling ib rasters and colour stats | cd .planning/2026-09-24-interior-twin/specs/full/services23f && python3 resample.py 301000 ib -100 130 -65 125 /tmp/nv/uv2f_ib.png && python
95. bash: Comparing Naver outline vs live | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > outline_cmp.py <<'EOF' import sys, numpy as np import matplotlib; matp
96. read: Viewing outline comparison | /tmp/nv/cmp2f.png
97. bash: Checking scipy/cv2 and voidscan2 | cd .planning/2026-09-24-interior-twin/specs/full/services23f && ../../../../../.tools/regtech/venv/bin/python -c "import scipy,cv2;print(sci
98. read: Viewing voidscan2 main-east crop | .planning/2026-09-26-full-twin/voidscan2/crop-main-east.png
99. read: Viewing fall-7-31 view | .planning/2026-09-26-full-twin/voidscan2/views/fall-7-31.png
100. bash: Running local ICP per region | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > icp.py <<'EOF' """Local rigid 2D ICP: Naver per-floor outline pixels (
101. bash: Testing fixed-rotation ICP | cd .planning/2026-09-24-interior-twin/specs/full/services23f && ../../../../../.tools/regtech/venv/bin/python icp.py '{"eastFix":{"raster":"
102. bash: ICP on box sub-regions | cd .planning/2026-09-24-interior-twin/specs/full/services23f && ../../../../../.tools/regtech/venv/bin/python icp.py '{"boxN":{"raster":"/tm
103. bash: Resampling corrected region rasters | cd .planning/2026-09-24-interior-twin/specs/full/services23f && /opt/homebrew/bin/python3 - <<'EOF' s=open('resample.py').read() s=s.replace
104. bash: Rendering corrected region overlays | cd .planning/2026-09-24-interior-twin/specs/full/services23f && cat > ov2.py <<'EOF' """Region overlay: corrected Naver raster (extent = box
105. read: Viewing corrected west overlay | /tmp/nv/ovw.png
106. bash: Fetching z21 raster west toilets | cd /tmp/nv && mkdir -p r21 && python3 - <<'EOF' import math, subprocess, concurrent.futures as cf, os, sys sys.path.insert(0,'/Users/um-yuns
107. read: Viewing z21 west raster | /tmp/nv/west21.png
108. read: Viewing corrected box-north overlay | /tmp/nv/ovbn.png
109. grep: Searching evidence-2f for services | .planning/2026-09-24-interior-twin/evidence-2f.json
110. read: Viewing corrected east overlay | /tmp/nv/ove.png
111. bash: Listing POIs near east end all floors | cd .planning/2026-09-24-interior-twin/specs/full/services23f && python3 -c " import json,nvuv S=json.load(open('/tmp/nv/summaries.json')) fo

## Its last 12 tool results (truncated)
- /tmp/nv/ov-2f-boxw.png /tmp/nv/ov-2f-east.png /tmp/nv/ov-2f-west.png /tmp/nv/ovbn.png /tmp/nv/ovbs.png /tmp/nv/ove.png /tmp/nv/ovw.png   Wall time: 9.79 seconds
- Read image file [image/webp]
- Read image file [image/webp]
- 9 (768, 768)   Wall time: 1.97 seconds
- Read image file [image/png]
- Read image file [image/png]
- Read image file [image/webp]
- Read image file [image/webp]
- [.planning/2026-09-24-interior-twin/evidence-2f.json#0192]  502:     { *503:      "owner": "Sign · 화장실",  504:      "category": "fascia/sign/panel",  505:      "renderers": 2,  506:      "tris": 0, ...  1566:     ], *1567:     "basis": "board 2F (2026-04 kgnwrite_004): 자동발매기 drawn inside the 역무실 column below 수유방 and above the 매표창구 bar; machines stand on the column front line u-3 (역무실/수유방 +u face) facing the concourse"  1568:    },  1569:    {  15
- Read image file [image/webp]
- Read image file [image/webp]
- 300000 2142736026 부산역(고속철도) 1F 남자화장실 62.8 -10.4 300000 2144256396 부산역(고속철도) 1F 엘리베이터 68.5 18.5 300000 2145282404 부산역(고속철도) 1F 엘리베이터 72.2 7.2 300000 2145667419 부산역(고속철도) 1F 에스컬레이터 84.5 17.7 301000 2145295463 부산역(고속철도) 2F 계단 78.4 17.6 301000 2145397134 부산역(고속철도) 2F 여자화장실 63.3 -8.5 301000 2145425557 부산역(고속철도) 2F 계단 72.4 11.2 301000 2143451207 Busan Station (Gosokcheoldo) 2F Men's Restroom 60.7 -13.8 301000 2143678978 Busan Station (Gosokcheoldo) 2F 