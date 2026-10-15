from pathlib import Path
import json, re, struct, hashlib, yaml
import numpy as np

OUT=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
checks=[]
def check(name, ok, detail=None):
    checks.append({'id':name,'passed':bool(ok),'detail':detail})
def blocks(path):
    return {int(i):(int(t),b) for t,i,b in re.findall(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',Path(path).read_text(),re.M|re.S)}
def obj(block): return next(iter(yaml.safe_load(block[1]).values()))
def mesh(path):
    text=Path(path).read_text();m=obj(next(iter(blocks(path).values())));vd=m['m_VertexData'];raw=bytes.fromhex(re.search(r'^\s+_typelessdata: ([0-9a-f]+)$',text,re.M).group(1))
    channels=vd['m_Channels'];active=[c for c in channels if c['dimension']]
    assert all(c['stream']==0 and c['format']==0 for c in active)
    stride=max(c['offset']+c['dimension']*4 for c in active)
    rows=np.frombuffer(raw,'<f4').reshape(vd['m_VertexCount'],stride//4)
    arr=[]
    for c in channels:
        arr.append(rows[:,c['offset']//4:c['offset']//4+c['dimension']].copy() if c['dimension'] else np.zeros((0,0),dtype='<f4'))
    dtype='<u4' if m['m_IndexFormat'] else '<u2';ib=bytes.fromhex(re.search(r'^\s+m_IndexBuffer: ([0-9a-f]+)$',text,re.M).group(1));indices=[]
    for s in m['m_SubMeshes']:
        indices.append(np.frombuffer(ib,dtype,s['indexCount'],s['firstByte']).astype('<i4')+s['baseVertex'])
    return m,arr,indices
def binary(path):
    b=Path(path).read_bytes();pos=0
    def i():
        nonlocal pos
        n=struct.unpack_from('<i',b,pos)[0];pos+=4;return n
    def a(dim):
        nonlocal pos
        n=i();v=np.frombuffer(b,'<f4',n*dim,pos).reshape(n,dim).copy();pos+=n*dim*4;return v
    assert i()==1
    arrays=[a(3),a(3),a(4),a(4)];uv=[]
    for _ in range(8):
        n=i();dim=i();v=np.frombuffer(b,'<f4',n*4,pos).reshape(n,4).copy();pos+=n*16;uv.append(v[:,:dim])
    arrays+=uv;subs=[]
    for _ in range(i()):
        top=i();n=i();v=np.frombuffer(b,'<i4',n,pos).copy();pos+=n*4;subs.append((top,v))
    assert pos==len(b)
    return arrays,subs

packet_path='workers/twin/output/floor2-whole-layout-20261009/native-fixed-stair-separation-packet.json'
packet=json.load(open(packet_path));reg=json.load(open('content/world/observed-interiors/native-architectural-parts.json'));row=reg['entries'][0]
current_scene='Assets/ChooGuard/Scenes/FpsStation.unity'
scene='workers/twin/evidence/floor2-facade-drafts-20261009/install-20261009T041703891Z/FpsStation.before.unity'
before='.planning/2026-10-09-floor2-completion-pass2/native-stair-integration/before-native-stair.unity'
A=blocks(scene);B=blocks(before);changed=[i for i in B.keys()&A.keys() if B[i]!=A[i]];added=list(A.keys()-B.keys());removed=list(B.keys()-A.keys())
check('sealed_packet_hash',sha(packet_path)==row['packet_sha256'])
check('exact_before_epoch',sha(before)==packet['scene_sha256'])
check('installed_scene_epoch',sha(scene)==reg['scene_sha256'],sha(scene))
check('only_declared_existing_components_changed',set(changed)=={829381565,829381566,829381568},changed)
check('one_new_child_components_only',len(added)==6 and sorted(A[i][0] for i in added)==[1,4,23,33,64,114] and not removed,{'added':added,'removed':removed})
for i in changed:
    old=obj(B[i]);new=obj(A[i]);key='m_Children' if A[i][0]==4 else 'm_Mesh';o={k:v for k,v in old.items() if k!=key};n={k:v for k,v in new.items() if k!=key}
    check('existing_component_preserved_'+str(i),o==n)
check('exact_single_child_transform',obj(A[1440906412])['m_LocalPosition']=={'x':0,'y':0,'z':0} and obj(A[1440906412])['m_LocalRotation']=={'x':0,'y':0,'z':0,'w':1} and obj(A[1440906412])['m_LocalScale']=={'x':1,'y':1,'z':1} and obj(A[1440906412])['m_Father']['fileID']==829381565)
ignore={'m_GameObject','m_ObjectHideFlags','m_CorrespondingSourceObject','m_PrefabInstance','m_PrefabAsset'}
for old_id,new_id,name in [(829381567,1440906415,'renderer'),(829381566,1440906414,'collider')]:
    o=obj(A[old_id]);n=obj(A[new_id]);skip=ignore|({'m_Mesh'} if name=='collider' else set())
    check('child_preserves_'+name+'_all_serialized_fields',{k:v for k,v in o.items() if k not in skip}=={k:v for k,v in n.items() if k not in skip})
check('source_cover_83_and_1306',len(row['source_objects'])==83 and len({v['source_id'] for v in row['source_objects']})==83 and sum(v['source_triangles'] for v in row['source_objects'])==1306)
check('source_metadata_exact',all({k:v[k] for k in ['source_id','source_path','source_world_matrix','source_triangles']}==r for v,r in zip(packet['source_objects'],row['source_objects'])))
identity=obj(A[1440906413]);check('single_static_identity_no_motion',identity['SourceId']=='93f4f5ec5996ef5390adf44a' and identity['SourcePath']=='ROOT/g62/g44/g1' and identity['PreservedTriangles']==1306 and identity['OperationStatus']=='static_native_architectural_part_no_motion' and row['guide_facility_id'] is None)
original_m,original_arr,original_idx=mesh(packet['renderer']['mesh']['asset'])
check('original_render_asset_unchanged',sha(packet['renderer']['mesh']['asset'])==packet['renderer']['mesh']['asset_sha256'])
for dep in row['dependencies']:
    tag=dep['role']+'_'+dep['part'];m,arr,idx=mesh(dep['asset']);expected=packet['complete_channels']['expected_partitions'][dep['role']][dep['part']];ea,ei=binary(expected['path'])
    check(tag+'_asset_pinned',sha(dep['asset'])==dep['file_sha256'])
    check(tag+'_expected_binary_pinned',sha(expected['path'])==expected['binary_sha256'])
    check(tag+'_channels_exact',len(arr)==14 and all((not len(a) and not len(e)) or np.array_equal(a,e) for a,e in zip(arr[:12],ea)) and all(not len(a) for a in arr[12:]))
    check(tag+'_serialized_vertex_layout_exact',m['m_VertexData']==original_m['m_VertexData'] and m['m_IndexFormat']==original_m['m_IndexFormat'])
    check(tag+'_68_exact_submesh_indices',len(idx)==len(ei)==68 and all(s['topology']==e[0] and np.array_equal(v,e[1]) for s,v,e in zip(m['m_SubMeshes'],idx,ei)))
    check(tag+'_face_count',sum(len(i) for i in idx)//3==expected['triangle_count'],expected['triangle_count'])
    p=arr[0][np.concatenate(idx)];mins=p.min(0);maxs=p.max(0);center=(mins+maxs)*.5;extent=(maxs-mins)*.5;bounds=m['m_LocalAABB'];actual=np.array([list(bounds['m_Center'].values()),list(bounds['m_Extent'].values())]);check(tag+'_referenced_bounds',np.max(np.abs(actual-np.array([center,extent])))<3e-7,float(np.max(np.abs(actual-np.array([center,extent])))))
for role in ['render','collider']:
    src=next(e for e in packet['complete_channels']['entries'] if e['role']==role);_,srcidx=binary(src['path']);_,ki=mesh(next(d['asset'] for d in row['dependencies'] if d['role']==role and d['part']=='retained'))[1:];_,ti=mesh(next(d['asset'] for d in row['dependencies'] if d['role']==role and d['part']=='selected'))[1:]
    selection=set(packet['complete_channels']['roles'][role]['selected_triangle_ordinals']);cursor=0;ok=True
    for (_,si),k,t in zip(srcidx,ki,ti):
        a=si.reshape(-1,3);mask=np.array([i in selection for i in range(cursor,cursor+len(a))],dtype=bool);cursor+=len(a);ok &= np.array_equal(a[~mask].ravel(),k) and np.array_equal(a[mask].ravel(),t)
    check(role+'_source_union_exact_disjoint_winding_preserved',ok and len(selection)==1306)
for mat in row['materials']:check('material_'+mat['guid'],sha(mat['path'])==mat['sha256'])

facade=json.load(open('workers/twin/output/floor2-facade-drafts-20261009/facade-install-plan.draft.json'));facade_summary=[]
check('facade_old_epoch_only_needs_repin',facade['scene_sha256']==packet['scene_sha256'])
for r in facade['meshes']:
    owner=int(r['owner_global_object_id'].split('-')[-2]);check('facade_owner_'+str(owner)+'_unchanged',A[owner]==B[owner])
    check('facade_source_'+str(owner)+'_pinned',sha(r['baseline_asset'])==r['baseline_file_sha256'] and sha(r['source_material'])==r['source_material_sha256'])
    m,a,ix=mesh(r['baseline_asset']);v=a[0];detail={'guide_unit':r['guide_unit'],'operation':r['operation'],'vertices':len(v),'triangles':sum(len(i) for i in ix)//3}
    if r['operation']=='letter_plane':
        uv=a[4];corner=lambda xy:np.where(np.linalg.norm(uv-np.array(xy),axis=1)<1e-5)[0][0];tl,tr,br,bl=[v[corner(xy)] for xy in [(0,1),(1,1),(1,0),(0,0)]];right=br-bl;up=tl-bl;normal=a[1][0]
        detail['board_width']=float(np.linalg.norm(right));detail['board_height']=float(np.linalg.norm(up));detail['uv_handedness']=float(np.dot(np.cross(right,-up),normal));check('facade_'+str(r['guide_unit'])+'_four_corner_rect',np.linalg.norm((tl+br)-(tr+bl))<1e-5 and abs(np.dot(right,up))<1e-5)
    if r['operation']=='partition_fascia':
        tri=ix[0].reshape(-1,3);sel=tri[:12];keep=tri[12:];detail['selected_bounds']=[v[sel].reshape(-1,3).min(0).tolist(),v[sel].reshape(-1,3).max(0).tolist()];detail['unit22_retained_bounds']=[v[keep].reshape(-1,3).min(0).tolist(),v[keep].reshape(-1,3).max(0).tolist()];check('unit21_no_unit22_vertices',not(set(sel.ravel()) & set(keep.ravel())) and len(tri)==24)
    facade_summary.append(detail)
result={'schema':'chooguard.independent-final-pass2-serialized-review.v1','unity_executed':False,'scene_snapshot':scene,'scene_sha256':sha(scene),'current_scene_sha256_at_finish':sha(current_scene),'before_scene_sha256':sha(before),'checks':checks,'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks),'facade_meshes':facade_summary}
(OUT/'serialized-review.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k!='checks'},ensure_ascii=False,indent=2))
print('FAILED',[c for c in checks if not c['passed']])
