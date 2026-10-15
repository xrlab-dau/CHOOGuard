#!/usr/bin/env python3
"""Plot actual saved scene wall sections in station UV for guide comparison."""
import json, math, struct
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from official_plan_compare import cut, readbin

BASE = Path('.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json')
OUT = Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout')
OUT.mkdir(parents=True, exist_ok=True)
scene = json.loads(BASE.read_text())
meshes = {x['id']: x for x in scene['meshes']}
s, c = math.sin(math.radians(16.2)), math.cos(math.radians(16.2))
rot = np.array([[s, c], [c, -s]])
cache = {}
font = ImageFont.truetype('/System/Library/Fonts/AppleSDGothicNeo.ttc', 15)
plan = json.loads(Path('.planning/2026-09-23-video-twin/majibang-plan.json').read_text())
ground = json.loads(Path('.planning/2026-09-23-video-twin/1f-implementation/interior-spec.json').read_text())
for floor, y in [(1, 1.5), (2, 8.5), (3, 13.7)]:
    lo, hi = np.array([-95., -70.]), np.array([125., 105.])
    scale = 7
    def point(p): return ((hi[0]-p[0])*scale, (hi[1]-p[1])*scale)
    im = Image.new('RGB', (int((hi[0]-lo[0])*scale), int((hi[1]-lo[1])*scale)), 'white')
    d = ImageDraw.Draw(im)
    for u in range(-90,126,10):
        d.line([point([u,lo[1]]),point([u,hi[1]])],fill='#eeeeee')
        d.text(point([u,hi[1]-1]),str(u),fill='#777777',font=font)
    for v in range(-70,106,10):
        d.line([point([lo[0],v]),point([hi[0],v])],fill='#eeeeee')
    records=[]
    for r in scene['renderers']:
        if not r['active'] or not r['renderer_enabled'] or not r['mesh'] or any(t in r['path'] for t in ['ShadowCaster','ShadowCap','KTXSource']): continue
        if r['world_bounds_min'] is None or not r['world_bounds_min'][1]<y<r['world_bounds_max'][1]:continue
        m=meshes[r['mesh']]
        if not m['binary']:continue
        if m['id'] not in cache:cache[m['id']]=readbin(m)
        v,t=cache[m['id']];wm=np.array(r['world_matrix']);world=v@wm[:3,:3].T+wm[:3,3]
        seg=cut(world[t],y)@rot
        use=(seg.max(1)>=lo).all(1)&(seg.min(1)<=hi).all(1);seg=seg[use]
        if not len(seg):continue
        native=r['path'].startswith('FPSWorld/')
        colour='#263f50' if native else '#bb522e'
        for line in seg:d.line([point(p) for p in line],fill=colour,width=1)
        records.append({'path':r['path'],'mesh':m['asset'],'segments':seg.tolist(),'native':native})
    units=ground['units'] if floor==1 else plan['secondFloorUnits'] if floor==2 else plan['thirdFloor']['units']
    for i,u in enumerate(units):
        if 'polygon' in u:p=np.array(u['polygon'])
        else:
            if None in u['v']: continue
            p=np.array([[u['u'][0],u['v'][0]],[u['u'][1],u['v'][0]],[u['u'][1],u['v'][1]],[u['u'][0],u['v'][1]]])
            if u.get('frame')=='box' or floor==3:
                theta=math.radians(plan['boxFrame']['rotDeg']);p=p@np.array([[math.cos(theta),math.sin(theta)],[-math.sin(theta),math.cos(theta)]])+plan['boxFrame']['origin']
        d.line([point(q) for q in np.vstack([p,p[0]])],fill='#007756',width=2)
        d.text(point(p.mean(0)),str(i+1)+' '+u['name'],fill='#00663b',font=font)
    d.rectangle((0,0,850,25),fill='white');d.text((5,2),f'{floor}F actual wall section Y={y}; blue=native, orange=added, green=current units',fill='black',font=font)
    im.save(OUT/f'floor{floor}-actual-section-before.png')
    (OUT/f'floor{floor}-actual-segments.json').write_text(json.dumps({'scene_sha256':scene['scene_file_sha256'],'section_y':y,'frame':'station UV, U right-to-left, V up','owners':records},ensure_ascii=False))
    print(floor,len(records),sum(len(r['segments']) for r in records))
