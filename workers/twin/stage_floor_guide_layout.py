#!/usr/bin/env python3
"""Stage exact per-vertex translation of existing 2F entrance tenancies."""
import json, hashlib, math
from pathlib import Path
import numpy as np
from official_plan_compare import readbin
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components

BASE=Path('.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json')
OUT=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout')
scene=json.loads(BASE.read_text()); meshes={m['id']:m for m in scene['meshes']}
plan=json.loads(Path('.planning/2026-09-23-video-twin/majibang-plan.json').read_text())
names=['다인명가','호두나니&부산오뎅','청도할매김밥','반월당닭강정']
units=[u for u in plan['secondFloorUnits'] if u['name'] in names]
proposal_path=Path('workers/twin/output/floor2-architecture-next-20261009/final-proposed-bounds.json')
proposal=json.loads(proposal_path.read_text())
proposals={u['current_unit_name']:u for u in proposal['units']}
s,c=math.sin(math.radians(16.2)),math.cos(math.radians(16.2))
def to_world(uvy):
    return np.column_stack([uvy[:,0]*s+uvy[:,1]*c,uvy[:,2],uvy[:,0]*c-uvy[:,1]*s])
def mapped(uvy, u):
    m=proposals[u['name']]['native_mapping'];out=uvy.copy();out[:,0]=out[:,0]*m['u_scale']+m['u_bias'];out[:,1]+=m['v_translation'];return out
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
cache={}; patches={}; movable=[]; rejects=[]
for r in scene['renderers']:
    p=r['path']
    if not r['active'] or not r['mesh'] or not (p.startswith('맞이방 · 원본 정합/') or p.startswith('실내 트윈 마감/2F 본관/')):continue
    if any('/문 · '+name+' 출입문/' in p for name in names):
        parent=p.split('/문짝')[0]
        if parent not in movable:movable.append(parent)
        continue
    m=meshes[r['mesh']]
    if not m['binary']:continue
    if m['id'] not in cache:cache[m['id']]=readbin(m)
    v,t=cache[m['id']];wm=np.array(r['world_matrix']);world=v@wm[:3,:3].T+wm[:3,3]
    uvy=np.column_stack([world[:,0]*s+world[:,2]*c,world[:,0]*c-world[:,2]*s,world[:,1]])
    assigned=np.full(len(v),-1,int); selected_tris=np.zeros(len(t),bool)
    for i,u in enumerate(units):
        lo=np.array([u['u'][0]-.35,u['v'][0]-.35,6.95]);hi=np.array([u['u'][1]+.35,u['v'][1]+.35,10.6])
        mask=((uvy>=lo)&(uvy<=hi)).all(1)
        contained=mask[t].all(1);chosen=np.unique(t[contained])
        if len(chosen):assigned[chosen]=i;selected_tris|=contained
    ids=np.flatnonzero(assigned>=0)
    if not len(ids):continue
    crossing=(assigned[t]>=0).any(1)&~selected_tris
    if crossing.any():
        rejects.append({'owner':p,'reason':'shared selected vertices also belong to unselected triangles','triangles':np.flatnonzero(crossing).tolist()});continue
    # Architecture fits the native side-bay depth. Fixture components retain shape.
    rigid=not any(z in m['name'] for z in ['_Walls','ShopCeiling'])
    target=uvy.copy()
    if rigid:
        # Weld by actual vertex positions; flat-shaded face copies share a device.
        _,weld=np.unique(np.round(uvy[ids],5),axis=0,return_inverse=True)
        id_to_weld=np.full(len(v),-1,int);id_to_weld[ids]=weld
        edges=id_to_weld[t[selected_tris]];a=edges[:,[0,1,2]].ravel();b=edges[:,[1,2,0]].ravel()
        _,labels=connected_components(coo_matrix((np.ones(len(a)),(a,b)),shape=(weld.max()+1,weld.max()+1)),directed=False)
        for component in np.unique(labels):
            vi=ids[labels[weld]==component];u=units[assigned[vi[0]]]
            if len(set(assigned[vi]))!=1:raise RuntimeError('fixture connects multiple tenancies')
            centre=uvy[vi].mean(0);target[vi]=uvy[vi]+mapped(centre[None,:],u)[0]-centre
    else:
        for i,u in enumerate(units):target[assigned==i]=mapped(uvy[assigned==i],u)
    replacement=(to_world(target[ids])-wm[:3,3])@np.linalg.inv(wm[:3,:3]).T
    key=m['asset']
    patch={'source_asset':key,'source_asset_guid':m['asset_guid'],'source_file_sha256':sha(key),'source_binary_sha256':m['binary_sha256'],'vertices':len(v),'triangles':len(t),'vertex_indices':ids.tolist(),'expected_positions':v[ids].tolist(),'replacement_positions':replacement.tolist(),'rigid_components':rigid,'selected_triangles':np.flatnonzero(selected_tris).tolist(),'owners':[{'path':p,'global_object_id':r['global_object_id'],'world_matrix':r['world_matrix']}]}
    if key in patches:
        old=patches[key]
        if old['vertex_indices']!=patch['vertex_indices'] or not np.allclose(old['replacement_positions'],replacement,atol=1e-6):rejects.append({'owner':p,'reason':'same asset different vertex mapping'});continue
        old['owners']+=patch['owners']
    else:patches[key]=patch
motions=[]
for parent in movable:
    u=next(u for u in units if '/문 · '+u['name']+' 출입문' in parent)
    front=u['u'][0] if u['face']=='-u' else u['u'][1]
    anchor=np.array([[front,sum(u['v'])/2,7.0]])
    motions.append({'path':parent,'unit':u['name'],'world_delta':(to_world(mapped(anchor,u))-to_world(anchor))[0].tolist()})
for u in units:
    anchor=np.array([[sum(u['u'])/2,sum(u['v'])/2,10.1]])
    motions.append({'path':'맞이방 · 원본 정합/'+u['name']+' fill light','unit':u['name'],'world_delta':(to_world(mapped(anchor,u))-to_world(anchor))[0].tolist()})
packet={'schema':'chooguard.floor-guide-layout-patch.v1','scene':'Assets/ChooGuard/Scenes/FpsStation.unity','scene_sha256':sha('Assets/ChooGuard/Scenes/FpsStation.unity'),'reference_only':True,'site_accuracy_verified':False,'guide':'asset-library/research-public/2026-10-09/floor2-architecture-next/guide03-kgnwrite-005-original.jpg','guide_sha256':sha('asset-library/research-public/2026-10-09/floor2-architecture-next/guide03-kgnwrite-005-original.jpg'),'proposal':str(proposal_path),'proposal_sha256':sha(proposal_path),'method':'Fit four detached entrance tenancies to native side bay; preserve fixture shapes and frontage spans. Reference layout, not field measurement.','units':[{'name':u['name'],'before':{'u':u['u'],'v':u['v']},'after':{'u':proposals[u['name']]['proposed_u'],'v':proposals[u['name']]['proposed_v']},'mapping':proposals[u['name']]['native_mapping']} for u in units],'mesh_patches':list(patches.values()),'movable_transforms':motions,'rejected_owners':rejects}
OUT.mkdir(parents=True,exist_ok=True);(OUT/'entrance2f-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'meshes':len(patches),'owners':sum(len(x['owners']) for x in patches.values()),'vertices':sum(len(x['vertex_indices']) for x in patches.values()),'doors':len(movable),'rejections':rejects},ensure_ascii=False))
