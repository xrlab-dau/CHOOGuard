# Resume digest for ShopInteriors (the previous agent was killed by a session interruption at 13:40:26, 2026-09-25)

Full previous transcript (grep it for details, e.g. measurements, URLs, frame times): ~/.omp/agent/sessions/-CHOOGuard/2026-09-22T09-47-00-085Z_01a0c882-f275-7000-a4e8-29539b53d97a/ShopInteriors.jsonl

## Files it wrote or edited
- .planning/2026-09-26-full-twin/kit-requests/ShopInteriors-1.json
- agent://KitExtend
- .planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py

## Messages it received
- [Wait interrupted by message] <irc from="parent" agent="Main"> Main: agent ids in this round are SeamPassage-2, PlatformSide-2, Public1F-2, Services23F-2, ShopInteriors, RegTech-2, KitExtend (write agent://<id>). check_spec.py already knows the plan.md contract types Room, ToiletRoom, Window, Canopy (Room/ToiletRoom walls are checked as mounted walls with openings left out; Window/Canopy as mounted). Main is rebuilding the majibang builder root shortly (GATE2 atrium guard fix) — renders taken in the next few minutes may show that change. </irc>
- [Wait interrupted by message] <irc from="parent" agent="Main"> Main: new contract kit type `Slab` (plan.md "New kit types"): walkable floor plate with collider — `polygon`, `holes?`, `y` = top | `depth` .3, `finish`, `edgeFinish`, `soffit`, `collider` true. Use it for any new walkable floor (passages, decks, landings); FloorFinish stays a collider-less overlay. check_spec.py knows Slab. Also: `.planning/2026-09-26-full-twin/voidscan2/voidscan2.json` + `voidscan2-{0,7,12}.png` is a collider-based scan (player-reachable floor, fall edges, doors/glass with nothing behind) — use its runs in your area as a checklist; Main is fixing the thin gap strips between the 2F cell floor and the facades its

## Its last reasoning (newest last, truncated)
- Public1F's role isn't really my concern here since units already exist. I'll move on to writing the generator carefully.
- I'm double-checking a few details: how the element lookup matches by unit and identity, that wall_edges for the 'side' case correctly excludes the back edge while 'all' includes it, and that the cooler's positioning calculation places its back face flush against the wall.  I'm also noticing the wall_run search loop is expensive since it recomputes buffer operations repeatedly instead of caching results by clearance value, so I should add caching there. The descending s1 loop breaking on the first valid hit is fine since it finds the longest run first, and I'm verifying grid centering math, the staff zone containment checks against buffered polygons, and the clearance/glass checks for counters.  Checking cash(), downlights() consistency with the front-edge extent logic, report slug naming conventions, and the budget loop that adjusts pitch before I manually lower caps if still over budget.
- I made an error with the line numbers—I accidentally replaced lines 403-405, which were actually the keep/glass checks. I need to go back and re-read that region carefully.
- 1F's estimated 123k exceeds budget even at pitch 3.0, so I need to load the report and print a per-unit breakdown of fixtures, tris, and notes to see where the overage is coming from.
- I'm checking the PHO-RAIL space — it's only 4 meters wide, and I'm working out whether the table footprint plus wall clearance actually fits within that width given the column spacing formula.  For a better fit, I should distribute columns evenly across the width based on how many fit, rather than using a fixed centered grid that might miss the space entirely.  Separately, the convenience store's middle gondola likely won't fit given the trapezoid's shrinking depth combined with the cooler and margin requirements, so I may need to shrink the gondola depth instead.  For the second floor units, several are failing the cash counter check because the door keep-out zone is eating too much depth—for open storefronts with wide entrances, I need to shrink the keep-out zone since customers can enter from anywhere along that wide front rather than treating the whole opening like a narrow door.  Trying a 1.2 m keep-out over the central strip instead of 2.0 m for open fronts. Checking 복순도가's shallow 2.7 m depth against this new keep-out plus cooler and counter clearances—it's tight but workable, then moving on to check the small box units.

## Every step it took (tool: intent | command head)
1. read: Reading full-twin plan | .planning/2026-09-26-full-twin/plan.md
2. read: Reading kit spec | .planning/2026-09-24-interior-twin/kit-spec.md
3. glob: Listing full-twin planning files | .planning/2026-09-26-full-twin/**
4. glob: Checking existing scratch outputs | .planning/2026-09-24-interior-twin/specs/full/**;asset-library/research-public/2026-09-26/**;.planning/2026-09-26-full-twin/kit-requests/**
5. bash: Listing specs directories | ls -la .planning/2026-09-24-interior-twin/specs/ .planning/2026-09-24-interior-twin/specs/enhance/ .planning/2026-09-24-interior-twin/specs/
6. bash: Listing enhance and audit dirs | ls .planning/2026-09-24-interior-twin/specs/enhance/ .planning/2026-09-25-interior-enhance/ .planning/2026-09-25-interior-enhance/audit/ .pl
7. eval:  | import json v=json.load(open('.planning/2026-09-25-interior-enhance/verification.json')) print(type(v), list(v.keys()) if isinstance(v,dict)
8. eval:  | for k in ['builds','stillC','openDecisions','renders']:     print(k, json.dumps(v[k],ensure_ascii=False,indent=0)[:2500]); print('---')
9. eval:  | sp=json.load(open('.planning/2026-09-23-video-twin/1f-implementation/interior-spec.json')) print(list(sp.keys())) for k,vv in sp.items():   
10. eval:  | for u in sp['units']:     d={k:u[k] for k in u if k not in ('polygon','corrections')}     print(json.dumps(d,ensure_ascii=False)[:900])     
11. grep: Searching interior.py for kind/fixture logic | .planning/2026-09-23-video-twin/1f-implementation/interior.py
12. grep: Finding 1F unit kind handling in C# | AgentScripts
13. read: Reading 1F unit builder | AgentScripts/MajibangBuilder.cs:470-640
14. read: Reading KitShop implementation | AgentScripts/MajibangBuilder.cs:3215-3400
15. grep: Finding existing ShopInterior uses | .planning
16. eval:  | e=json.load(open('.planning/2026-09-24-interior-twin/specs/enhance/2f-main-enhance.json')) print(list(e.keys()) if isinstance(e,dict) else '
17. bash: Scanning check_spec structure | wc -l .planning/2026-09-24-interior-twin/specs/enhance/check_spec.py; grep -n "ShopInterior\|def \|argparse\|add_argument\|sys.argv" .planni
18. read: Reading check_spec header and Live | .planning/2026-09-24-interior-twin/specs/enhance/check_spec.py:1-160
19. read: Reading check_spec volumes and check | .planning/2026-09-24-interior-twin/specs/enhance/check_spec.py:185-456
20. grep: Checking KitExtend progress on kit code | AgentScripts/MajibangBuilder.cs
21. grep: Listing kit prop models | AgentScripts/MajibangBuilder.cs
22. read: Viewing 1F compare sheet | .planning/2026-09-25-interior-enhance/audit/compare-1f.jpg
23. eval:  | d1=json.load(open('.planning/2026-09-24-interior-twin/specs/1f-detail.json')) print({k:(d1[k] if k!='elements' else len(d1[k])) for k in d1}
24. bash: Listing 1F evidence acquisition | ls .planning/2026-09-23-video-twin/1f-evidence-acquisition/ | cat; ls .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/ .pla
25. read: Viewing unit-08 overlay | .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/unit-08.jpg
26. read: Viewing unit-01 overlay | .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/unit-01.jpg
27. read: Viewing unit-03 overlay | .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/unit-03.jpg
28. eval:  | for c in sp['corrections']:     print(json.dumps(c,ensure_ascii=False)[:1200]); print()
29. bash: Inspecting interior.py Audit API | grep -n "class Audit\|    def \|^def " .planning/2026-09-23-video-twin/1f-implementation/interior.py | cat; ls .planning/2026-09-23-video-tw
30. eval:  | import sys, os os.chdir('/Users/um-yunsang/CHOOGuard') sys.path.insert(0,'.planning/2026-09-23-video-twin/1f-implementation') sys.path.inser
31. eval:  | rows=list(A.active_renderers()) print(len(rows), rows[0].keys()) paths=[r['path'] for r in rows] tops=Counter('/'.join(p.split('/')[:2]) for
32. eval:  | mp=json.load(open('.planning/2026-09-23-video-twin/majibang-plan.json')) print(list(mp.keys())) su=mp['secondFloorUnits'] print(len(su), su[
33. read: Reading builder 2F unit Shop function | AgentScripts/MajibangBuilder.cs:1130-1295
34. eval:  | bs=json.load(open('.planning/2026-09-24-interior-twin/specs/box-3f/box-storefronts.json')) print({k:(bs[k] if k!='elements' else len(bs[k]))
35. read: Viewing 2F box / 3F compare sheet | .planning/2026-09-25-interior-enhance/audit/compare-2f-box-3f.jpg
36. eval:  | tf=mp['thirdFloor'] print(type(tf), list(tf.keys()) if isinstance(tf,dict) else len(tf)) for k,vv in tf.items():     s=json.dumps(vv,ensure_
37. eval:  | for u in tf['units']:     print(json.dumps(u,ensure_ascii=False)[:700]) print() s3=json.load(open('.planning/2026-09-24-interior-twin/specs/
38. eval:  | fx=sorted(set(p.split('/')[1] for p in paths if p.startswith('맞이방 · 원본 정합/') and re.search(r' (fixture|display|chair|table)$', p.split('/')[
39. read: Viewing unit-04 overlay | .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/unit-04.jpg
40. read: Viewing unit-07 overlay | .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/unit-07.jpg
41. read: Viewing unit-09 overlay | .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/unit-09.jpg
42. read: Viewing unit-10 overlay | .planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/unit-10.jpg
43. eval:  | ob=json.load(open('.planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/observations.json')) print(type(ob), list(ob.keys())[:20
44. eval:  | for f in ob['topologyFindings']: print('-',f) print() for c in ob['components']:     print(json.dumps(c,ensure_ascii=False)[:900]); print() 
45. bash: Listing research-public asset folders | cd asset-library/research-public; for d in 2026-09-24/busan-1f 2026-09-24/official-first-floor 2026-09-24/shop-fascia-main 2026-09-24/shop-f
46. bash: Inspecting busan-1f research folder | cd asset-library/research-public/2026-09-24/busan-1f; ls photos contact-sheets media frames recovered-crops | head -80; python3 -c " import 
47. bash: Summarizing busan-1f photos | cd asset-library/research-public/2026-09-24/busan-1f; ls photos | sed 's/_[0-9]*\.\(jpg\|png\)$//' | sort | uniq -c; ls photos | wc -l; ls r
48. glob: Locating May 2026 1F photos | asset-library/**/may2026-1f-photo-*
49. eval:  | from PIL import Image, ImageDraw import glob def sheet(files, out, cols=4, w=480, label=True):     ims=[]     for f in files:         im=Ima
50. read: Viewing May 2026 contact sheet | /tmp/shopint/may2026.jpg
51. grep: Checking render entry arguments | AgentScripts/CaptureRegisteredScene.cs
52. eval:  | import math, subprocess, time import numpy as np S,C=math.sin(math.radians(16.2)),math.cos(math.radians(16.2)) def w(u,v,y): return [u*S+v*C
53. read: Viewing unit 8 before render | .planning/2026-09-26-full-twin/shop-work/renders/before-1f-u08.png
54. read: Reading KCooler end | AgentScripts/MajibangBuilder.cs:3404-3415
55. bash: Checking kit-requests dir | ls -la .planning/2026-09-26-full-twin/ .planning/2026-09-26-full-twin/kit-requests 2>&1 | head -30
56. write: Writing ShopInterior kit extension request | .planning/2026-09-26-full-twin/kit-requests/ShopInteriors-1.json
57. write: Notifying KitExtend of request | agent://KitExtend
58. eval:  | ev=json.load(open('.planning/2026-09-24-interior-twin/evidence-2f.json')) print(type(ev), list(ev.keys()) if isinstance(ev,dict) else len(ev
59. bash: Listing boxgeo helpers | grep -n "^def \|^[A-Z_]* = " .planning/2026-09-24-interior-twin/specs/box-3f/boxgeo.py | head -40
60. eval:  | m2=json.load(open('.planning/2026-09-24-interior-twin/specs/2f-main.json')) print({k:(m2[k] if k!='elements' else len(m2[k])) for k in m2}) 
61. eval:  | import boxgeo from shapely.geometry import Polygon, Point, LineString, box as sbox def sf_index(spec):     out=[]     for el in spec['elemen
62. eval:  | def match_fronts(poly, zone):     P=Polygon(poly); out=[]     for s in SF[zone]:         m=((s['a'][0]+s['b'][0])/2,(s['a'][1]+s['b'][1])/2)
63. eval:  | def main_front(it):     fr=it['fronts']     withdoor=[f for f in fr if f['doors']]     return (withdoor or fr)[0] if fr else None def slug(i
64. bash: Checking render outputs | ls -la .planning/2026-09-26-full-twin/shop-work/renders | head; ls .planning/2026-09-26-full-twin/shop-work/renders | wc -l
65. eval:  | def lvl_sheet(level, prefix, out, cols=4, w=400):     fs=[f"{RD}/renders/{prefix}-{slug(it)}.png" for it in inv if it['level']==level and os
66. read: Viewing 1F before sheet | .planning/2026-09-26-full-twin/shop-work/sheet-before-1f.jpg
67. read: Viewing 2F main before sheet | .planning/2026-09-26-full-twin/shop-work/sheet-before-2f-main.jpg
68. read: Viewing 2F box before sheet | .planning/2026-09-26-full-twin/shop-work/sheet-before-2f-box.jpg
69. read: Viewing 3F before sheet | .planning/2026-09-26-full-twin/shop-work/sheet-before-3f.jpg
70. eval:  | ov='.planning/2026-09-23-video-twin/1f-evidence-acquisition/sy0-walk/overlays/' fs=[ov+f for f in ['unit-11.jpg','unit-12.jpg','unit-13.jpg'
71. read: Viewing overlay sheet 2 | /tmp/shopint/ov2.jpg
72. eval:  | fs=sorted(glob.glob('asset-library/research-public/2026-09-24/busan-1f/photos/2025-02-13_zimcarry*')) print(sheet(fs[:32],'/tmp/shopint/zim.
73. read: Viewing zimcarry photos | /tmp/shopint/zim.jpg
74. eval:  | vs=json.load(open('.planning/2026-09-26-full-twin/voidscan2/voidscan2.json')) print(list(vs.keys()) if isinstance(vs,dict) else len(vs)) for
75. eval:  | print(Counter(r['kind'] for r in vs['runs'])) lvmap={'1F':0.0,'2F main':7.0,'2F box':7.0,'3F':12.2} for it in inv:     P=Polygon(it['poly'])
76. eval:  | for n in ['seen-7-27','seen-7-9','seen-7-7','fall-7-21','seen-7-22','fall-7-20','seen-7-21','seen-12-2','seen-12-3','seen-7-28','seen-7-24']
77. eval:  | def unit_by(key): return [i for i in inv if i['key']==key][0] def front_index(poly, f):     # edge index whose endpoints match storefront ed
78. bash: Reading validate output | ls .planning/2026-09-26-full-twin/shop-work/; cat .planning/2026-09-26-full-twin/shop-work/test-auto*.validate.json 2>/dev/null | head -60
79. eval:  | def geom_front(poly, fi):     n=len(poly); P=np.array(poly,float)     area=0.5*sum(P[i,0]*P[(i+1)%n,1]-P[(i+1)%n,0]*P[i,1] for i in range(n)
80. grep: Finding prop asset base paths | AgentScripts/MajibangBuilder.cs
81. bash: Checking prop asset formats | cd "Assets/ChooGuard/Art/StationInterior/Kits"; ls restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/ | cat; ls "restaurant-bits/KayKit
82. eval:  | def obj_tris(path):     n=0     for line in open(path,errors='ignore'):         if line.startswith('f '): k=len(line.split())-1; n+=k-2     
83. eval:  | for c in ev['contradictions']:     if c['id'] in ('CX-04','CX-20','CX-05'): print(json.dumps(c,ensure_ascii=False)[:2500]); print() br=json.
84. eval:  | c=[c for c in ev['contradictions'] if c['id']=='CX-04'][0] print(json.dumps(c['evidenceValue'],ensure_ascii=False)) print(json.dumps({k:c[k]
85. eval:  | cg='asset-library/research-public/2026-09-24/metric-controls/current-geometry/' print(sheet([cg+'may2026-1f-photo-04.jpg',cg+'may2026-1f-pho
86. read: Viewing photos 04 and 10 | /tmp/shopint/p0410.jpg
87. eval:  | for p,o in walk(ev):     if isinstance(o,dict) and o.get('id') in ('M-BOKSOONDOGA-INTERIOR','M-KKOTDEUL-INTERIOR','M-BOKSOONDOGA-APERTURE','
88. eval:  | import check_spec as CS t=time.time() live=CS.Live('.planning/2026-09-23-video-twin/1f-implementation/geometry-after15') print('load',round(
89. eval:  | rows15=list(interior.Audit('.planning/2026-09-23-video-twin/1f-implementation/geometry-after15').active_renderers()) p15=[r['path'] for r in
90. read: Reading KitExtend answer | .planning/2026-09-26-full-twin/kit-requests/ShopInteriors-1.done.json
91. grep: Locating KShopLayout | AgentScripts/MajibangBuilder.cs
92. read: Reading KShopLayout body | AgentScripts/MajibangBuilder.cs:3339-3385
93. read: Reading KitProp placement semantics | AgentScripts/MajibangBuilder.cs:2397-2430
94. bash: Verifying evidence file paths | ls asset-library/research-public/2026-09-22/busan-station-interior/floorplans/ 2>&1 | head; ls asset-library/research-public/2026-09-23/busa
95. read: Viewing box shop footage RY2 390-419 | asset-library/research-public/2026-09-23/busan-sinmajibang/contact-sheets/RY2qvE0Tugk_00390-00419.jpg
96. bash: Preparing full specs dir | mkdir -p .planning/2026-09-24-interior-twin/specs/full && ls .planning/2026-09-24-interior-twin/specs/full; /opt/homebrew/bin/python3 -c "im
97. bash: Checking python deps | /opt/homebrew/bin/python3 -c "import PIL, shapely, numpy; print(PIL.__version__, shapely.__version__)"; ls .planning/2026-09-24-interior-twi
98. write: Writing shop interiors generator | .planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py
99. edit: Caching polygon buffers in Planner.free | [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#13D2] PUT 403.=405: +    def free(self, fp, margin, clearance=.25, wal
100. read: Re-reading free() region | .planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py:392-415
101. edit: Fixing free() with caching and keep-out checks | [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#DE3C] CUT 399.=402 PUT 409.=410: +            return False +        if
102. TEXT Now add `self._inner = {}` in `__init__`.
103. grep: Locating init line | .planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py
104. edit: Adding buffer cache init | [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#B186] PUT 356.=356: +        self.tris = 0 +        self._inner = {} 
105. bash: Running generator first pass | cd .planning/2026-09-24-interior-twin/specs/full && time /opt/homebrew/bin/python3 make_shop_interiors.py
106. eval:  | FD=ROOTP+'/.planning/2026-09-24-interior-twin/specs/full/' rep=json.load(open(FD+'shop-interiors-report.json')) for lv in ['shops-1f.json','

## Its last 12 tool results (truncated)
- Read image file [image/webp] [Image: original 1920x970, displayed at 1568x792. Multiply coordinates by 1.22 to map to original image.]
- Read image file [image/webp] [Image: original 1920x970, displayed at 1568x792. Multiply coordinates by 1.22 to map to original image.]
- infill.json public1f services23f Traceback (most recent call last):   File "<string>", line 1, in <module>     import matplotlib, shapely; print(matplotlib.__version__, shapely.__version__)     ^^^^^^^^^^^^^^^^^^^^^^^^^^ ModuleNotFoundError: No module named 'matplotlib'   Wall time: 0.22 seconds  Command exited with code 1
- 12.2.0 2.1.2 .planning/2026-09-24-interior-twin/specs/full/public1f: __pycache__ check_registration.py liveq.py mvt.py naver-1f-uv.json plot_naver_live.py register_naver.py registration-check.json    Wall time: 0.29 seconds
- [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#13D2] Successfully wrote 44473 bytes to .planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py
- [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#DE3C] 399:    def free(self, fp, margin, clearance=.25, wall=False): 400:        inner = self.P.buffer(-.02 if wall else -clearance) 401:        if not inner.contains(fp): 402:            return False 403:    def free(self, fp, margin, clearance=.25, wall=False): 404:        key = -.02 if wall else -clearance … 407:            inner = self._inner[key] = self.P.buffer(key) 408: 
- [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#DE3C] 381:    def footprint(self, fix, at, heading, length=None): 382:        h = np.asarray(heading, float) / np.linalg.norm(heading) … 391:        lx, dz = size[fix] 392:        return self.oriented(at, side, h, lx, dz) 393: 394:    def cost(self, fix, length=None): 395:        if fix in SHELF_PER_M: 396:            return int(SHELF_PER_M[fix] * (length or 2.0) + 60) 397:    
- [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#B186] 398: 399:    def free(self, fp, margin, clearance=.25, wall=False): 400:        key = -.02 if wall else -clearance 401:        inner = self._inner.get(key) 404:        if not inner.contains(fp): 405:            return False 406:        if any(fp.intersects(k) for k in self.keep): 407:            return False 408:        if not wall and fp.intersects(self.glass): 409:     
- [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#B186]  354:        self.back = max(cand, key=lambda i: (float(np.dot(self.mid(i) - fa, self.inn)) + .15 * self.length(i))) if cand else None *355:        self.layout, self.placed, self.notes = [], [], []  356:        self.tris = 0  357:  358:    # geometry helpers
- [.planning/2026-09-24-interior-twin/specs/full/make_shop_interiors.py#EBD6] 316:    def __init__(self, it, cfg): 317:        self.it, self.cfg = it, cfg  355:        self.layout, self.placed, self.notes = [], [], [] 356:        self.tris = 0 357:        self._inner = {} 358: 359:    # geometry helpers
- shops-1f.json: 16 interiors, ~123059 tris, downlightPitch 3.0 shops-2f-main.json: 4 interiors, ~25712 tris, downlightPitch 2.0 shops-2f-box.json: 6 interiors, ~41794 tris, downlightPitch 2.0 shops-3f.json: 1 interiors, ~13414 tris, downlightPitch 2.0  real	0m1.759s user	0m3.282s sys	0m0.505s   Wall time: 1.77 seconds
- SHOP-1F-01 retail front 3 open [4] back 1 y1 3.0 {'counter': 3, 'shelf': 2, 'plant': 1} [] SHOP-1F-02 convenience front 3 open [4] back 0 y1 3.0 {'cooler': 1, 'counter': 1, 'shelf': 1, 'gondola': 1} [] SHOP-1F-03 bakery front 10 open [0, 1, 6] back 2 y1 3.0 {'counter': 3, 'menu': 1, 'bakeryShelf': 1, 'tableSet': 5, 'plant': 1} [] SHOP-1F-04 retail front 8 open [1, 5, 6] back 4 y1 3.0 {'counter': 1, 'shelf': 1, 'gondola': 4} [] SHOP-1F-05 bakery f