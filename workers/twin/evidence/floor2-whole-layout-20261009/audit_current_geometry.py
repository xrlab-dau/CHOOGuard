from pathlib import Path
import json,hashlib,sys,struct
import numpy as np
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from PIL import Image,ImageDraw

E=Path(__file__).resolve().parent
O=Path('workers/twin/output/floor2-whole-layout-20261009')
P=Path('.planning/2026-10-09-floor2-completion-pass2/current/current-inventory.json')
S=json.loads(P.read_text());MM={m['id']:m for m in S['meshes']}
F=np.array(json.loads(Path('workers/twin/output/floor2-main-restoration-20261009/whole-guide-current-audit.json').read_text())['native_station_frame']);I=np.linalg.inv(F)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
cache={}
def geo(r):
    m=MM[r['mesh']]
    if m['id']not in cache:
        b=Path(m['binary']).read_bytes();assert hashlib.sha256(b).hexdigest()==m['binary_sha256']
        nv,ni,nn=struct.unpack_from('<iii',b);v=np.frombuffer(b,'<f4',nv*3,12).reshape(nv,3);t=np.frombuffer(b,'<i4',ni,12+nv*12).reshape(-1,3)
        cache[m['id']]=(v,t)
    v,t=cache[m['id']];M=np.array(r['world_matrix']);q=((v@M[:3,:3].T+M[:3,3])@I.T)[:,[2,0,1]]
    return v,t,q
selected=[];walls=[];rows=[]
for r in S['renderers']:
    if not r.get('mesh')or not r['active']or not r['renderer_enabled']or not MM[r['mesh']].get('binary'):continue
    if not (r['path'].startswith('FPSWorld/공식 자료 부산역 역사/')or('계단' in r['path']and'KTX'not in r['path'])):continue
    v,t,q=geo(r);tri=q[t];lo=tri.min(1);hi=tri.max(1);cent=tri.mean(1);n=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);area=np.linalg.norm(n,axis=1)/2
    locality=(hi[:,0]>-105)&(lo[:,0]<85)&(hi[:,1]>-55)&(lo[:,1]<112)
    horizontal=(hi[:,2]-lo[:,2]<.003)&(cent[:,2]>.08)&(cent[:,2]<7.01)&(area>.025)&(area<5)&locality
    faceids=np.flatnonzero(horizontal)
    if len(faceids):
        selected.append(tri[faceids]);rows.append({'owner':r,'mesh':MM[r['mesh']],'triangle_indices':faceids.tolist(),'sample_start':sum(len(x)for x in selected[:-1]),'triangles':len(faceids)})
    vertical=(hi[:,2]>8.25)&(lo[:,2]<8.25)&locality
    if vertical.any():
        ts=tri[vertical];segs=[]
        for a,b in[(0,1),(1,2),(2,0)]:
            va,vb=ts[:,a],ts[:,b];dy=vb[:,2]-va[:,2];valid=(abs(dy)>1e-8)&(((va[:,2]<=8.25)&(vb[:,2]>=8.25))|((vb[:,2]<=8.25)&(va[:,2]>=8.25)))
            f=np.divide(8.25-va[:,2],dy,out=np.zeros(len(dy)),where=abs(dy)>1e-8);pp=va+(vb-va)*f[:,None];segs.append((pp,valid))
        for a,b in[(0,1),(0,2),(1,2)]:
            ok=segs[a][1]&segs[b][1];walls.append(np.stack([segs[a][0][ok,:2],segs[b][0][ok,:2]],1))
tris=np.concatenate(selected);cent=tris.mean(1)
# Connections use geometry centres; they generate inspection clusters only, never facility identities.
tree=cKDTree(cent);pairs=tree.query_pairs(1.8,output_type='ndarray');pairsel=abs(cent[pairs[:,0],2]-cent[pairs[:,1],2])<.6;pairs=pairs[pairsel]
graph=coo_matrix((np.ones(len(pairs)),(pairs[:,0],pairs[:,1])),shape=(len(cent),len(cent)));ncomp,label=connected_components(graph,directed=False)
clusters=[]
for k in range(ncomp):
    ix=np.flatnonzero(label==k);p=tris[ix].reshape(-1,3);levels=np.unique(np.round(cent[ix,2],2))
    if len(ix)<8 or len(levels)<6 or np.ptp(p[:,2])<1:continue
    owners=[]
    for row in rows:
        mask=(ix>=row['sample_start'])&(ix<row['sample_start']+row['triangles'])
        if mask.any():owners.append(row|{'cluster_triangle_indices':[row['triangle_indices'][int(i-row['sample_start'])]for i in ix[mask]]})
    clusters.append({'id':len(clusters),'native_UVY_bounds':[p.min(0).tolist(),p.max(0).tolist()],'centroid':cent[ix].mean(0).tolist(),'horizontal_triangle_count':len(ix),'level_count':len(levels),'levels_model_Y':levels.tolist(),'owners':owners})
im=Image.new('RGB',(1520,1400),'#f8f6ee');draw=ImageDraw.Draw(im)
def px(q):return (int((85-q[0])*7.5+30),int((112-q[1])*7.5+30))
for seg in np.concatenate(walls):draw.line([px(x)for x in seg],fill='#71808b',width=1)
for tri,y in zip(tris,cent[:,2]):draw.polygon([px(x)for x in tri],fill=(int(45+20*y),int(80+20*y),150))
for c in clusters:
    u,v,y=c['centroid'];draw.text(px((u,v)),str(c['id']),fill='red')
guide=json.loads(Path('workers/twin/output/floor2-main-restoration-20261009/facility-native-controls.json').read_text())
for n in guide['facility_nodes']:
    if n['id']not in guide['summary']['unresolved_unique_source_assignment']:continue
    b=n['native_control_bounds_UV'];draw.line([px(x)for x in[(b[0],b[1]),(b[2],b[1]),(b[2],b[3]),(b[0],b[3]),(b[0],b[1])]],fill='#851515',width=1);draw.text(px((b[0],b[3])),n['id'],fill='#851515')
draw.text((30,10),'Current original-source horizontal levels Y0.08..7.01, source wall section Y8.25. Red windows are inspection only.',fill='#20313f');im.save(O/'current-circulation-geometry.png')
out={'inventory':str(P),'inventory_sha256':sha(P),'scene_sha256':S['scene_file_sha256'],'native_frame':F.tolist(),'source_rows':rows,'clusters':clusters,'interpretation':'Repeated horizontal source levels are inspection candidates. Shape, guide topology and contributor identity must independently agree.'}
(E/'current-circulation-clusters.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print('CLUSTERS',[(c['id'],np.round(c['native_UVY_bounds'],2).tolist(),c['horizontal_triangle_count'],c['level_count'])for c in clusters])
