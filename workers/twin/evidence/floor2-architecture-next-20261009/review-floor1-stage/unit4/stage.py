#!/usr/bin/env python3
"""Stage the guide's ARTBOX setback, preserving native frontage and floor support."""
import hashlib, json, math, re
from pathlib import Path
import numpy as np
from shapely.geometry import Polygon
from shapely import contains_xy
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from official_plan_compare import readbin

WORK=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout')
BASE=WORK/'current-before-unit4/current-scene-structure.json'
PROPOSAL=Path('workers/twin/output/floor1-multiview-next-20261009/remaining-guide-coverage/conservative-proposals4.json')
ATTRIBUTION=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage/unit4/fixture-attribution-masks.json')
LIGHTS=ATTRIBUTION.with_name('facade-downlight-masks.json')
FIXED=ATTRIBUTION.with_name('fixed-frontage-masks.json')
doc=json.loads(BASE.read_text());proposal=json.loads(PROPOSAL.read_text());unit=proposal['operations'][0]
old=np.array(unit['current_polygon_UV']);new=np.array(unit['proposed_polygon_UV']);poly=Polygon(old).buffer(.35)
s,c=math.sin(math.radians(16.2)),math.cos(math.radians(16.2))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def uv(w):return np.column_stack([w[:,0]*s+w[:,2]*c,w[:,0]*c-w[:,2]*s])
def world_delta(d):return np.column_stack([d[:,0]*s+d[:,1]*c,np.zeros(len(d)),d[:,0]*c-d[:,1]*s])
anchor=-34.939
def interior(q):
    x=q[:,0];front=np.interp(x,old[[0,8,7],0],old[[0,8,7],1])
    du=np.interp(x,old[[0,8,7],0],(new-old)[[0,8,7],0])
    f=np.maximum(0,(q[:,1]-anchor)/(front-anchor))
    return q+np.column_stack([du*f,(new[0,1]-front)*f])
def map_points(q):
    result=interior(q)
    # Keep the real 1.8m apertures and transverse material thickness. The
    # native plaza end of edge6 stays fixed; the aisle end retreats along it.
    nearest=np.full(len(q),np.inf)
    for edge in [0,6,7,8]:
        a,b=old[edge],old[(edge+1)%len(old)];A,B=new[edge],new[(edge+1)%len(new)]
        length=np.linalg.norm(b-a);target=np.linalg.norm(B-A);along=(b-a)/length
        t=(q-a)@along;foot=a+t[:,None]*along;distance=np.linalg.norm(q-foot,axis=1)
        if edge in [6,8]:
            at=9.677 if edge==6 else 3.354
            centre=a+along*at;moved=interior(centre[None])[0]
            target_at=np.dot(moved-A,(B-A)/target)
            before=[0,at-.9,at+.9,length];after=[0,target_at-.9,target_at+.9,target]
            if edge==6:
                hinge=(anchor-a[1])/along[1]
                before.insert(1,hinge);after.insert(1,hinge)
            assert np.all(np.diff(after)>0), 'Aperture profile folds or leaves no side support'
            mapped=np.interp(t,before,after)
            # Extend the profile beyond an endpoint without changing its cap thickness.
            mapped=np.where(t<0,t,mapped);mapped=np.where(t>length,target+t-length,mapped)
        else:mapped=t*target/length
        eligible=(t>=-.4)&(t<=length+.4)&(distance<.65)&(distance<nearest)
        if edge==0:eligible &= q[:,1]>anchor+1e-7
        result[eligible]=A+mapped[eligible,None]*(B-A)/target+(q-foot)[eligible]
        nearest[eligible]=distance[eligible]
    result[q[:,1]<=anchor+1e-7]=q[q[:,1]<=anchor+1e-7]
    for fixed in old[1:7]:
        same=np.linalg.norm(q-fixed,axis=1)<.00001
        result[same]=q[same]
    return result

assert np.max(np.abs(map_points(old)-new))<1e-8
meshes={m['id']:m for m in doc['meshes']};patches={};motions={};rejects=[];cache={}
attribution=json.loads(ATTRIBUTION.read_text())
# Reviewer-provided masks identify whole authored assemblies across body,
# product, joint and collider batches; panel centroids are never independent.
fixture_masks={}
for r in attribution['mesh_mask_rows']:
    fixture_masks.setdefault(r['source_asset'],{'assemblies':[]})['assemblies'].append(r)
for r in json.loads(LIGHTS.read_text())['mesh_mask_rows']:
    if not r['preserve_fixed_frontage']:
        fixture_masks.setdefault(r['source_asset'],{'assemblies':[]})['assemblies'].append(r)
fixed_masks={}
for r in json.loads(FIXED.read_text())['mesh_mask_rows']:
    fixed_masks.setdefault(r['source_asset'],set()).update(r['vertex_indices']+r['exact_coincident_referenced_aliases'])
shop=json.loads(Path('content/world/observed-interiors/shops-1f.json').read_text())
layout=next(e for e in shop['elements'] if e['id']=='SHOP-1F-04')['params']['layout']
fixture_delta={int(f['sourceIndex']):world_delta(map_points(np.array([f['at']]))-np.array([f['at']]))[0] for f in layout if f['fixture']=='gondola'}
# The first full-size gondola must be inset rather than protrude into the freed aisle.
f2=np.array(next(f['at'] for f in layout if f['sourceIndex']==2));p2=map_points(f2[None])[0]
inset=max(0,p2[1]+1.23-(new[0,1]-.08))
fixture_delta[2]+=world_delta(np.array([[0.,-inset]]))[0]
rows=doc['renderers']+[dict(r,collider_only=True) for r in doc['colliders'] if r.get('type')=='MeshCollider' and r['path'].endswith('/Kit_Colliders')]
for r in rows:
    p=r['path'];m=meshes.get(r.get('mesh'))
    if not m or not r['active']:continue
    if not any(p.startswith(root) for root in ['1층 · 원본 정합/1층 실내 · 근거 표기/','실내 트윈 마감/1F/','실내 트윈 마감/점포 내부 1F/']):continue
    if p.startswith('실내 트윈 마감/점포 내부 1F/SHOP-1F-'):
        if not p.startswith('실내 트윈 마감/점포 내부 1F/SHOP-1F-04 '):continue
        name='/'.join(p.split('/')[:3]);centre=uv(np.array([[r['world_matrix'][i][3] for i in range(3)]]))
        motions[name]={'kind':'path','path':name,'world_delta':world_delta(map_points(centre)-centre)[0].tolist(),'boardNo':4}
        continue
    if '/문 · ' in p:continue
    if '_Walls' in p and p!=unit['dedicated_wall_owner']['path']:continue
    # Keep the existing flush floor under both the smaller store and released aisle.
    if any(tag in p for tag in ['Ground_Floor','Kit_Floor_']):continue
    if p.startswith('실내 트윈 마감/1F/') and not r.get('collider_only') and m['asset'] not in fixture_masks and not any(tag in p.split('/')[2] for tag in ['Glass','Frame','Fascia','Lightbox','Trim','Glyph','Clad','Joint']):continue
    if not m.get('binary'):continue
    if m['id'] not in cache:cache[m['id']]=readbin(m)
    v,t=cache[m['id']];wm=np.array(r['world_matrix']);world=v@wm[:3,:3].T+wm[:3,3];q=uv(world)
    _,weld=np.unique(np.round(v,5),axis=0,return_inverse=True)
    edges=weld[t[:,[0,1,2]]].ravel();nxt=weld[t[:,[1,2,0]]].ravel()
    _,wl=connected_components(coo_matrix((np.ones(len(edges)),(edges,nxt)),shape=(weld.max()+1,weld.max()+1)),directed=False);labels=wl[weld]
    selected=np.zeros(len(t),bool)
    dedicated=p==unit['dedicated_wall_owner']['path']
    inside=contains_xy(poly,q[:,0],q[:,1])&(world[:,1]>-.08)&(world[:,1]<5.01)
    for component in np.unique(labels[t[:,0]]):
        faces=np.flatnonzero(labels[t[:,0]]==component);vi=np.unique(t[faces])
        if dedicated or inside[vi].all():selected[faces]=True
    mask=fixture_masks.get(m['asset'])
    if mask:
        # This exact attribution supersedes spatial selection for procedural fixtures.
        # Joint batches also contain enclosure toes/backers; retain their owned selection.
        if not p.endswith('/Kit_Joint'):selected[:]=False
        for assembly in mask['assemblies']:selected[assembly['selected_triangles']]=True
    if not selected.any():continue
    ids=np.unique(t[selected]);cross=(np.isin(t,ids).any(1))&~selected
    if cross.any():rejects.append({'owner':p,'reason':'selected vertices cross unselected faces'});continue
    mapped=map_points(q[ids]);delta=world_delta(mapped-q[ids])
    if mask:
        selected_index={int(index):j for j,index in enumerate(ids)}
        for assembly in mask['assemblies']:
            source=assembly.get('sourceIndex')
            rigid=fixture_delta.get(source)
            if source==1:
                a,b=old[0],old[1];A,B=new[0],new[1]
                along=(b-a)/np.linalg.norm(b-a)
                chosen=np.array([selected_index[index] for index in assembly['vertex_indices']]);points=q[ids[chosen]]
                run=(points-a)@along;foot=a+run[:,None]*along
                mapped=A+run[:,None]*(B-A)/np.linalg.norm(b-a)+(points-foot)
                delta[chosen]=world_delta(mapped-points)
            if 'fixture_id' in assembly:
                centre=np.array([assembly['source_center_UV']]);rigid=world_delta(map_points(centre)-centre)[0]
            if rigid is not None:
                for index in assembly['vertex_indices']:delta[selected_index[index]]=rigid
    for j,index in enumerate(ids):
        if int(index) in fixed_masks.get(m['asset'],set()):delta[j]=0
    moving=np.linalg.norm(delta,axis=1)>1e-7
    if not moving.any():continue
    replacement=v[ids].astype(float)+np.linalg.solve(wm[:3,:3],delta.T).T
    key=m['asset'];owner={'path':p,'global_object_id':r['global_object_id'],'world_matrix':r['world_matrix']}
    row={'source_asset':key,'source_asset_guid':m['asset_guid'],'source_file_sha256':sha(key),'source_binary_sha256':m['binary_sha256'],'vertices':len(v),'triangles':len(t),'vertex_indices':ids.tolist(),'expected_positions':v[ids].tolist(),'replacement_positions':replacement.tolist(),'selected_triangles':np.flatnonzero(selected).tolist(),'owners':[] if r.get('collider_only') else [owner]}
    if r.get('collider_only'):row['collider_owners']=[owner]
    if key in patches:
        if patches[key]['vertex_indices']!=row['vertex_indices'] or not np.allclose(patches[key]['replacement_positions'],replacement,atol=1e-7):rejects.append({'owner':p,'reason':'shared mesh mapping differs'})
        else:patches[key]['owners']+=row['owners']
    else:patches[key]=row

for edge,at in [(6,9.677),(8,3.354)]:
    a,b=old[edge],old[(edge+1)%len(old)];centre=a+(b-a)*at/np.linalg.norm(b-a)
    key='1F-unit04-front'+str(edge)
    motions[key]={'kind':'door','door_element':key,'world_delta':world_delta(map_points(centre[None])-centre[None])[0].tolist(),'boardNo':4}
packet={'schema':'chooguard.floor-guide-layout-patch.v1','batch_id':'unit4','floor':1,'scene':'Assets/ChooGuard/Scenes/FpsStation.unity','scene_sha256':sha('Assets/ChooGuard/Scenes/FpsStation.unity'),'reference_only':True,'site_accuracy_verified':False,'guide':proposal['guide_native_image'],'guide_sha256':proposal['guide_image_sha256'],'proposal':str(PROPOSAL),'proposal_sha256':sha(PROPOSAL),'fixture_attribution':str(ATTRIBUTION),'fixture_attribution_sha256':sha(ATTRIBUTION),'additional_evidence':[{'path':str(p),'sha256':sha(p)} for p in [LIGHTS,FIXED]],'method':'Guide ratio setback along fixed native wall lines; retain full flush floor and fixed plaza frontage, preserve exact door aperture widths and rigid gondola/counter sizes; shorten coherent wall shelf.','units':[{'boardNo':4,'name':'ARTBOX','before':{'polygon':old.tolist()},'after':{'polygon':new.tolist()},'vertex_mapping':'fixed native frontage; explicit facade aperture profiles; weighted aisle setback'}],'mesh_patches':list(patches.values()),'movable_transforms':list(motions.values()),'rejected_owners':rejects}
(WORK/'unit4-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'meshes':len(patches),'vertices':sum(len(p['vertex_indices']) for p in patches.values()),'instances':len(motions),'rejections':rejects}))
