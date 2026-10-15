"""Byte-matched read-only native architectural observations in the 2F entry band."""
from pathlib import Path
import json, hashlib, struct
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path('/Users/um-yunsang/CHOOGuard')
OUT=ROOT/'workers/twin/evidence/floor2-architecture-next-20261009'
SCENE=ROOT/'.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json'
s=json.loads(SCENE.read_text()); meshes={m['id']:m for m in s['meshes']}
theta=np.deg2rad(16.2)
world_from_station=np.array([[np.cos(theta),0,np.sin(theta),0],[0,1,0,0],[-np.sin(theta),0,np.cos(theta),0],[0,0,0,1]])
station_from_world=np.linalg.inv(world_from_station)
cache={}
def read(mid):
    if mid not in cache:
        m=meshes[mid]; b=(ROOT/m['binary']).read_bytes()
        assert hashlib.sha256(b).hexdigest()==m['binary_sha256']
        nv,ni,nn=struct.unpack_from('<iii',b)
        assert len(b)==12+12*nv+4*ni+12*nn
        v=np.frombuffer(b,'<f4',offset=12,count=3*nv).reshape(-1,3)
        t=np.frombuffer(b,'<i4',offset=12+12*nv,count=ni).reshape(-1,3)
        cache[mid]=(v,t)
    return cache[mid]

canvas=Image.new('RGB',(1400,1250),'white');draw=ImageDraw.Draw(canvas);observations=[]
fontpath=ROOT/'Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf'
font=ImageFont.truetype(str(fontpath),19) if fontpath.exists() else ImageFont.load_default()
def px(u,v):return ((31-u)/44*1300+50,(-20-v)/41*1100+50)
for u in range(-10,31,5):
    draw.line([px(u,-61),px(u,-20)],fill='#eeeeee');draw.text(px(u,-20),str(u),fill='black',font=font)
for v in range(-60,-19,5):
    draw.line([px(-13,v),px(31,v)],fill='#eeeeee');draw.text(px(31,v),str(v),fill='black',font=font)
for r in s['renderers']:
    if not r['active'] or not r['renderer_enabled']:continue
    if not r['path'].startswith('FPSWorld/공식 자료 부산역 역사/MainShell'):continue
    m=meshes[r['mesh']]
    if not m['binary']:continue
    v,t=read(r['mesh']); p=(np.c_[v,np.ones(len(v))]@(station_from_world@np.array(r['world_matrix'])).T)[:,:3]
    tri=p[t];lo=tri.min(1);hi=tri.max(1)
    mask=(hi[:,2]>-12)&(lo[:,2]<30)&(hi[:,0]>-48)&(lo[:,0]<-20)&(hi[:,1]>7.05)&(lo[:,1]<10.7)
    ids=np.flatnonzero(mask)
    facets=[]
    for i in ids:
        q=tri[i]; n=np.cross(q[1]-q[0],q[2]-q[0]);length=np.linalg.norm(n)
        if length<1e-7:continue
        n=n/length
        if abs(n[1])>.4:continue
        if q[:,1].max()-q[:,1].min()<.3:continue
        # Station-native P(u,v,y)=(v,y,u), hence plan axes are (u=z,v=x).
        uv=q[:,[2,0]]
        pairs=[(0,1),(1,2),(2,0)];a,b=max(pairs,key=lambda ab:np.linalg.norm(uv[ab[1]]-uv[ab[0]]))
        if np.linalg.norm(uv[b]-uv[a])<.1:continue
        draw.line([px(*uv[a]),px(*uv[b])],fill='#c2c9ce',width=1)
        facets.append({'triangle_ordinal':int(i),'indices':t[i].tolist(),'native_xyz':q.tolist(),'normal_native_xyz':n.tolist()})
    if facets:
        observations.append({'owner':r,'mesh':m,'facets':facets})

plan=json.loads((ROOT/'.planning/2026-09-23-video-twin/majibang-plan.json').read_text())
names=['꽃들','복순도가','다인명가','호두나니&부산오뎅','청도할매김밥','반월당닭강정']
for u in plan['secondFloorUnits']:
    if u['name'] not in names:continue
    x0,x1=u['u'];y0,y1=u['v'];c='#008aab' if u['name'] in names[:2] else '#ce4b38'
    draw.line([px(x0,y0),px(x1,y0),px(x1,y1),px(x0,y1),px(x0,y0)],fill=c,width=3)
    draw.text(px((x0+x1)/2,(y0+y1)/2),u['name'],fill=c,font=font)
for g in plan['doorOpenings']:
    if g.get('region')=='Original front vestibule inner doors opened':
        for o in g['openings']:
            x0,x1=o['u'];y0,y1=o['v'];draw.line([px(x0,y0),px(x1,y0),px(x1,y1),px(x0,y1),px(x0,y0)],fill='#1eaa55',width=4)
draw.text((50,1180),'Native U is inverted to guide-left/right; V down is exterior. Source geometry, no field measurement.',fill='black',font=font)
canvas.save(OUT/'native-entry-plan.png')
(OUT/'native-entry-facets.json').write_text(json.dumps({'schema':'chooguard.native-entry-facet-audit.v1','role':'source_geometry_not_measurement','scene_inventory_sha256':hashlib.sha256(SCENE.read_bytes()).hexdigest(),'scene_export_sha256':s['scene_file_sha256'],'native_frame_world_matrix':world_from_station.tolist(),'filtered_region':{'u':[-12,30],'v':[-48,-20],'y':[7.05,10.7]},'observations':observations,'byte_matched_mesh_count':len(cache)},ensure_ascii=False,indent=2))
print(json.dumps({'owners':len(observations),'facets':sum(len(x['facets']) for x in observations),'byte_matched_meshes':len(cache)}))
