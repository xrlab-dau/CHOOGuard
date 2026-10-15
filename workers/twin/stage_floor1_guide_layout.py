#!/usr/bin/env python3
"""Stage 1F rear-row meshes and instance poses from the photographed floor guide."""
import json, re, math, hashlib, sys
from pathlib import Path
import numpy as np
from shapely.geometry import Polygon, Point
from shapely import contains_xy
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from official_plan_compare import readbin

SECOND='--unit16' in sys.argv
BASE=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/current-before-unit16/current-scene-structure.json' if SECOND else '.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json')
PROPOSAL=Path('workers/twin/output/floor1-multiview-next-20261009/remaining-guide-coverage/conservative-proposals16.json' if SECOND else 'workers/twin/output/floor1-multiview-next-20261009/conservative-proposals.json')
OUT=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout')
scene=json.loads(BASE.read_text());meshes={m['id']:m for m in scene['meshes']}
proposal=json.loads(PROPOSAL.read_text());units=proposal['operations']
by_number={int(u['boardNo']):i for i,u in enumerate(units)}
s,c=math.sin(math.radians(16.2)),math.cos(math.radians(16.2))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def uv(world):return np.column_stack([world[:,0]*s+world[:,2]*c,world[:,0]*c-world[:,2]*s])
def delta_world(d):return np.array([c*d,0.,-s*d])
deltas=[np.mean(np.array(u.get('unmodified_source_restore_polygon_UV',u['proposed_polygon_UV']))-u['current_polygon_UV'],axis=0)[1] for u in units]
polys=[Polygon(u['current_polygon_UV']).buffer(.35) for u in units]
spec=json.loads(Path('.planning/2026-09-23-video-twin/1f-implementation/interior-spec.json').read_text())
signs={Path(u['fasciaTex']).stem:by_number[int(u['boardNo'])] for u in spec['units'] if int(u['boardNo']) in by_number}
patches={};motions={};cache={};rejects=[]
rows=scene['renderers']+([dict(r,collider_only=True) for r in scene['colliders'] if r.get('type')=='MeshCollider' and r['path'].endswith('/Kit_Colliders')] if SECOND else [])
for r in rows:
    p=r['path'];m=meshes.get(r['mesh'])
    if m is None or not r['active']:continue
    owned=(p.startswith('1층 · 원본 정합/1층 실내 · 근거 표기/') or p.startswith('실내 트윈 마감/1F/') or p.startswith('실내 트윈 마감/점포 내부 1F/'))
    if not owned:continue
    fixture=re.search(r'^실내 트윈 마감/점포 내부 1F/(SHOP-1F-(\d\d) [^/]+)',p)
    if fixture and int(fixture[2]) in by_number:
        n=by_number[int(fixture[2])];path='실내 트윈 마감/점포 내부 1F/'+fixture[1]
        motions[path]={'kind':'path','path':path,'world_delta':delta_world(deltas[n]).tolist(),'boardNo':int(fixture[2])};continue
    if '/문 · ' in p:
        # Exact Element field replaces the ambiguous localized duplicate door names.
        doorasset=Path(m['asset']).name
        match=re.match(r'(1F-unit(\d\d)-front\d+)-',doorasset)
        if match and int(match[2]) in by_number:
            n=by_number[int(match[2])];motions[match[1]]={'kind':'door','door_element':match[1],'world_delta':delta_world(deltas[n]).tolist(),'boardNo':int(match[2])}
        continue
    if not m['binary']:continue
    if p.startswith('실내 트윈 마감/1F/') and not r.get('collider_only') and not any(k in p.split('/')[2] for k in ['Glass','Frame','Fascia','Lightbox','Trim','Glyph','Clad','Joint']):continue
    if m['id'] not in cache:cache[m['id']]=readbin(m)
    v,t=cache[m['id']];wm=np.array(r['world_matrix']);world=v@wm[:3,:3].T+wm[:3,3];uvs=uv(world)
    assigned=np.full(len(v),-1,int);face_labels=np.full(len(t),-1,int)
    ownwalls=[i for i,u in enumerate(units) if p==u['dedicated_wall_owner']['path']]
    ownsigns=[i for name,i in signs.items() if name in p]
    if p.startswith('1층 · 원본 정합/') and '_Walls' in p and not ownwalls:continue
    if ownwalls:face_labels[:]=ownwalls[0]
    elif ownsigns:face_labels[:]=ownsigns[0]
    else:
        _,weld=np.unique(np.round(v,5),axis=0,return_inverse=True)
        edges=weld[t[:,[0,1,2]]].ravel();next_edges=weld[t[:,[1,2,0]]].ravel()
        _,weld_labels=connected_components(coo_matrix((np.ones(len(edges)),(edges,next_edges)),shape=(weld.max()+1,weld.max()+1)),directed=False)
        labels=weld_labels[weld]
        within=[contains_xy(poly,uvs[:,0],uvs[:,1]) & (world[:,1]>-.08)&(world[:,1]<5.01) for poly in polys]
        for component in np.unique(labels[t[:,0]]):
            faceids=np.flatnonzero(labels[t[:,0]]==component);vi=np.unique(t[faceids]);centre=uvs[vi].mean(0)
            eligible=[j for j in range(len(units)) if within[j][vi].all()]
            if not eligible:continue
            best=min(eligible,key=lambda j:np.linalg.norm(centre-np.array(units[j]['current_polygon_UV']).mean(0)))
            face_labels[faceids]=best
    selected=face_labels>=0
    if not selected.any():continue
    conflicts=[]
    for ti in np.flatnonzero(selected):
        ids=t[ti];i=face_labels[ti]
        if ((assigned[ids]>=0)&(assigned[ids]!=i)).any():conflicts.append(int(ti))
        assigned[ids]=i
    ids=np.flatnonzero(assigned>=0);crossing=(assigned[t]>=0).any(1)&~selected
    if conflicts or crossing.any():
        rejects.append({'owner':p,'reason':'shared vertices cross unit or outside selected faces','conflicts':conflicts,'crossing':np.flatnonzero(crossing).tolist()});continue
    replacement=v[ids].copy().astype(float)
    for i,d in enumerate(deltas):
        sel=assigned[ids]==i;replacement[sel]+=np.linalg.solve(wm[:3,:3],delta_world(d))
    # Snap the shared 14/15 party edge instead of retaining the source's 0.188m² overlap.
    # Only architectural surfaces follow this tiny edge correction; fixtures stay rigid.
    if 14 in by_number and any(z in p for z in ['_Walls','ShopWall','ShopCeiling','Kit_Floor','Ground_ShopCeiling']):
        i=by_number[14];old=np.array(units[i]['current_polygon_UV']);new=np.array(units[i]['proposed_polygon_UV']);sel=assigned[ids]==i
        for j in np.flatnonzero(sel):
            q=uvs[ids[j]];a,b=old[2],old[3];f=np.clip(np.dot(q-a,b-a)/np.dot(b-a,b-a),0,1)
            foot=a+f*(b-a)
            if np.linalg.norm(q-foot)<.12:
                correction=(new[2]+f*(new[3]-new[2]))-(foot+[0,deltas[i]])
                worldcorr=np.array([correction[0]*s+correction[1]*c,0,correction[0]*c-correction[1]*s])
                replacement[j]+=np.linalg.solve(wm[:3,:3],worldcorr)
    key=m['asset'];patch={'source_asset':key,'source_asset_guid':m['asset_guid'],'source_file_sha256':sha(key),'source_binary_sha256':m['binary_sha256'],'vertices':len(v),'triangles':len(t),'vertex_indices':ids.tolist(),'expected_positions':v[ids].tolist(),'replacement_positions':replacement.tolist(),'selected_triangles':np.flatnonzero(selected).tolist(),'owners':[{'path':p,'global_object_id':r['global_object_id'],'world_matrix':r['world_matrix']}]}
    if r.get('collider_only'):patch['collider_owners']=patch.pop('owners');patch['owners']=[]
    if key in patches:
        if patches[key]['vertex_indices']!=ids.tolist() or not np.allclose(patches[key]['replacement_positions'],replacement,atol=1e-6):rejects.append({'owner':p,'reason':'shared mesh requires different mapping'});continue
        patches[key]['owners']+=patch['owners']
    else:patches[key]=patch

if not SECOND:
    # The old BOH return continued into the restored guide corridor. Replace only
    # its selected cladding/joint faces and its separately baked collision plane.
    boh_path=Path('workers/twin/output/floor1-multiview-next-20261009/boh-segment-replacement.json')
    boh=json.loads(boh_path.read_text())
    def existing_or_patch(owner, collider=False):
        m=meshes[owner['mesh']];v,t=readbin(m);key=m['asset']
        if key not in patches:
            patches[key]={'source_asset':key,'source_asset_guid':m['asset_guid'],'source_file_sha256':sha(key),'source_binary_sha256':m['binary_sha256'],'vertices':len(v),'triangles':len(t),'vertex_indices':[],'expected_positions':[],'replacement_positions':[],'selected_triangles':[],'owners':[]}
        row=patches[key]
        proof={'path':owner['path'],'global_object_id':owner['global_object_id'],'world_matrix':owner['world_matrix']}
        if collider:row['collider_owners']=[proof]
        elif proof not in row['owners']:row['owners'].append(proof)
        row['boh_evidence']=str(boh_path);row['boh_evidence_sha256']=sha(boh_path)
        return row,v,t
    for op in boh['owners']:
        owner=next(r for r in scene['renderers'] if r['global_object_id']==op['owner']['global_object_id'])
        row,v,t=existing_or_patch(owner)
        removed=op['old_segment_face_ordinals'];row['removed_triangle_ordinals']=removed
        affected=set(op['affected_vertex_indices']);keep=np.delete(t,removed,axis=0)
        assert not affected.intersection(keep.ravel()), 'BOH selected vertices shared with preserved faces'
        assert not affected.intersection(row['vertex_indices']), 'BOH patch conflicts with row movement'
        if op['replacement_box_localXYZ']:
            points=np.array(op['replacement_box_localXYZ']);reuse=op['affected_vertex_indices'][:24]
            quads=[[0,4,5,1],[2,3,7,6],[0,2,6,4],[1,5,7,3],[0,1,3,2],[4,6,7,5]]
            row['attribute_overrides']=[];row['added_triangles']=[]
            for k,q in enumerate(quads):
                xyz=points[q];tangent=xyz[1]-xyz[0];tangent/=np.linalg.norm(tangent)
                normal=np.cross(xyz[1]-xyz[0],xyz[2]-xyz[0]);normal/=np.linalg.norm(normal)
                w=np.linalg.norm(xyz[1]-xyz[0]);h=np.linalg.norm(xyz[3]-xyz[0])
                uv0=[[0.,0.],[w,0.],[w,h],[0.,h]]
                vi=reuse[k*4:k*4+4]
                for j,idx in enumerate(vi):
                    row['vertex_indices'].append(idx);row['expected_positions'].append(v[idx].tolist());row['replacement_positions'].append(xyz[j].tolist())
                    row['attribute_overrides'].append({'index':idx,'normal':normal.tolist(),'tangent':[*tangent,1.],'uv0':uv0[j]})
                row['added_triangles'] += [[vi[0],vi[1],vi[2]],[vi[0],vi[2],vi[3]]]
        row['operation_note']=op['operation']
    col=boh['collider_update'];row,v,t=existing_or_patch(col['owner'],collider=True)
    for entry in col['mapped_source_vertices']:
        row['vertex_indices'].append(entry['index']);row['expected_positions'].append(entry['current_localXYZ']);row['replacement_positions'].append(entry['proposed_localXYZ'])
    row['selected_triangles'] += [f['face_ordinal'] for f in col['source_segment_faces']]
    
    generic_path=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage/generic-collider-candidates.json')
    generic=json.loads(generic_path.read_text())
    assert generic['source_inventory_sha256']==sha(BASE)
    for candidate in generic['mesh_patch_candidates']:
        assert sha(candidate['source_asset'])==candidate['source_file_sha256']
        row=dict(candidate)
        if 'collider_owners' not in row:row['collider_owners']=row.pop('owners')
        row['owners']=[]
        for owner in row['collider_owners']:
            if 'owner' in owner and 'path' not in owner:owner['path']=owner.pop('owner')
        row['boh_evidence']=str(generic_path);row['boh_evidence_sha256']=sha(generic_path)
        assert row['source_asset'] not in patches
        patches[row['source_asset']]=row
packet={'schema':'chooguard.floor-guide-layout-patch.v1','floor':1,'scene':'Assets/ChooGuard/Scenes/FpsStation.unity','scene_sha256':sha('Assets/ChooGuard/Scenes/FpsStation.unity'),'reference_only':True,'site_accuracy_verified':False,'guide':proposal['guide_native_image'],'guide_sha256':proposal['guide_image_sha256'],'proposal':str(PROPOSAL),'proposal_sha256':sha(PROPOSAL),'method':'Restore guide food-corridor proportions and rear row adjacency by undoing the explicitly rejected camera-registration shifts. Native entrance/exterior/core unchanged.','units':[{'boardNo':u['boardNo'],'name':u['name'],'before':{'polygon':u['current_polygon_UV']},'after':{'polygon':u['proposed_polygon_UV']},'world_delta':delta_world(deltas[i]).tolist()} for i,u in enumerate(units)],'mesh_patches':list(patches.values()),'movable_transforms':list(motions.values()),'rejected_owners':rejects}
packet['batch_id']='unit16' if SECOND else 'floor1'
OUT.mkdir(exist_ok=True,parents=True);(OUT/('unit16-packet.json' if SECOND else 'floor1-packet.json')).write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'meshes':len(patches),'owners':sum(len(p['owners']) for p in patches.values()),'vertices':sum(len(p['vertex_indices']) for p in patches.values()),'instances':len(motions),'rejections':rejects},ensure_ascii=False))
