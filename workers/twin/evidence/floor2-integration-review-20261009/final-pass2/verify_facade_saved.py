from pathlib import Path
import json,re,hashlib,struct,yaml
import numpy as np

OUT=Path(__file__).resolve().parent
helpers=OUT/'verify_serialized.py';env={'__file__':str(helpers)};exec(helpers.read_text().split('packet_path=')[0],env)
sha,blocks,obj,mesh=env['sha'],env['blocks'],env['obj'],env['mesh']
before='workers/twin/evidence/floor2-facade-drafts-20261009/install-20261009T041703891Z/FpsStation.before.unity';current='Assets/ChooGuard/Scenes/FpsStation.unity'
snap=Path(current).read_bytes();scene_hash=hashlib.sha256(snap).hexdigest();A={int(i):(int(t),b) for t,i,b in re.findall(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',snap.decode(),re.M|re.S)};B=blocks(before)
reg=json.load(open('content/world/observed-interiors/observed-facades.json'));checks=[];summ=[]
def ck(name,ok,detail=None):checks.append({'id':name,'passed':bool(ok),'detail':detail})
go={i:obj(v) for i,v in A.items() if v[0]==1}
trans={i:obj(v) for i,v in A.items() if v[0]==4}
go_to_tr={v['m_GameObject']['fileID']:i for i,v in trans.items()}
def path_of_tr(i):
    t=trans[i];name=go[t['m_GameObject']['fileID']]['m_Name'];parent=t['m_Father']['fileID'];return path_of_tr(parent)+'/'+name if parent in trans else name
paths={path_of_tr(i):i for i in trans}
def components(trid,ctype):
    gid=trans[trid]['m_GameObject']['fileID'];return [(i,obj(A[i])) for i in [c['component']['fileID'] for c in go[gid]['m_Component']] if i in A and A[i][0]==ctype]
ck('source_plan_hash',sha(reg['source_plan'])==reg['source_plan_sha256'])
ck('six_mesh_four_text_five_door_scope',[len(reg[k]) for k in ['meshes','texts','doors']]==[6,4,5])
source_ids=[i for i,v in B.items() if v[0]==114 and 'ChooGuard.App.Fps.Twin.SourcePartIdentity' in v[1]]
ck('all_original_source_identities_unchanged',all(A.get(i)==B[i] for i in source_ids),len(source_ids))
photo_count=0
def photo(parent_tr,r,unit):
    global photo_count
    if not r:return
    photo_count+=1;tr=paths[path_of_tr(parent_tr)+'/'+r['child_name']];t=trans[tr]
    ck('photo_'+str(unit)+'_identity_pose',t['m_LocalPosition']=={'x':0,'y':0,'z':0} and t['m_LocalRotation']=={'x':0,'y':0,'z':0,'w':1})
    ck('photo_'+str(unit)+'_scale',all(abs(t['m_LocalScale'][k]-r['local_scale'][k])<2e-7 for k in 'xyz'))
    cs=components(tr,64)+components(tr,65)+components(tr,136)+components(tr,143);ck('photo_'+str(unit)+'_no_collision',not cs)
    mf=components(tr,33)[0][1];mr=components(tr,23)[0][1]
    ck('photo_'+str(unit)+'_mesh_material_binding',mf['m_Mesh']['guid']==r['mesh_guid'] and mr['m_Materials'][0]['guid']==r['material_guid'] and mr['m_Enabled']==1)
    ck('photo_'+str(unit)+'_original_jpg_bytes',sha(r['texture_asset'])==sha(r['source_artifact'])==r['photo_sha256'])
    ck('photo_'+str(unit)+'_asset_hashes',sha(r['mesh_asset'])==r['mesh_file_sha256'] and sha(r['material_asset'])==r['material_file_sha256'])
    m,a,ix=mesh(r['mesh_asset']);ck('photo_'+str(unit)+'_grid_125_vertices_192_faces',len(a[0])==125 and len(ix)==1 and len(ix[0])==576)
    verts=a[0];normals=a[1];uv=a[4];tri=ix[0].reshape(-1,3);cross=np.cross(verts[tri[:,1]]-verts[tri[:,0]],verts[tri[:,2]]-verts[tri[:,0]]);dots=np.einsum('ij,ij->i',cross,normals[tri[:,0]])
    ck('photo_'+str(unit)+'_winding_matches_front_normal',np.all(dots>0),float(dots.min()))
    ck('photo_'+str(unit)+'_uv_in_bounds',np.all(np.isfinite(uv)) and np.all(uv>=0) and np.all(uv<=1))
    q=np.array(r['original_plane_quad_px'],float);expected=q/np.array([r['image_width'],r['image_height']]);expected[:,1]=1-expected[:,1]
    actual=uv[[0,24,124,100]];ck('photo_'+str(unit)+'_all_four_uv_corners_exact',float(np.max(abs(actual-expected)))<8e-8,float(np.max(abs(actual-expected))))
    size=np.linalg.norm(verts[24]-verts[0])/np.linalg.norm(verts[100]-verts[0]);ck('photo_'+str(unit)+'_reference_aspect_preserved',abs(size-r['reference_aspect'])<3e-5,float(size))
for r in reg['meshes']:
    trid=paths[r['owner']];mf=components(trid,33)[0][1];mr=components(trid,23)[0][1];expected_guid=r.get('replacement_mesh_guid',r['baseline_guid'])
    ck('unit'+str(r['guide_unit'])+'_'+r['operation']+'_mesh_binding',mf['m_Mesh']['guid']==expected_guid)
    ck('unit'+str(r['guide_unit'])+'_'+r['operation']+'_source_pinned',sha(r['baseline_asset'])==r['baseline_file_sha256'] and sha(r['source_material_asset'])==r['source_material_file_sha256'])
    expected_mat=[r['source_material_guid'],r['replacement_material_guid']] if 'replacement_mesh_guid' in r else [r['replacement_material_guid']]
    ck('unit'+str(r['guide_unit'])+'_'+r['operation']+'_materials',[v['guid'] for v in mr['m_Materials']]==expected_mat)
    if r['operation']=='partition_fascia':
        original,oa,oi=mesh(r['baseline_asset']);new,na,ni=mesh(r['replacement_mesh_asset']);ck('unit21_full_channels_unchanged',new['m_VertexData']==original['m_VertexData'])
        ck('unit21_black_only_first12_unit22_second12_preserved',len(ni)==2 and np.array_equal(ni[0],oi[0][36:]) and np.array_equal(ni[1],oi[0][:36]))
    photo(trid,r.get('photo'),r['guide_unit'])
for r in reg['texts']:
    trid=paths[r['owner']];text=components(trid,102)[0][1];mr=components(trid,23)[0][1]
    ck('text_'+str(r['guide_unit'])+'_content',text['m_Text']==r['replacement_text'])
    ck('text_'+str(r['guide_unit'])+'_semantic_visibility',mr['m_Enabled']==(0 if r.get('photo') else 1))
    photo(trid,r.get('photo'),r['guide_unit'])
doors={}
for i,v in A.items():
    if v[0]==114 and 'ChooGuard.App.Fps.Facilities.StationDoor' in v[1]:
        d=obj(v);doors[d['Element']]=(i,d)
for r in reg['doors']:
    i,d=doors[r['element']];old=obj(B[i]);ck('door_'+str(r['guide_unit'])+'_only_label_changed',d['Label']==r['replacement_label'] and {k:v for k,v in d.items() if k!='Label'}=={k:v for k,v in old.items() if k!='Label'})
ck('exactly_four_photo_planes',photo_count==4,photo_count)
ck('native_stair_six_components_unchanged_after_facade',all(A[i]==B[i] for i in [1440906411,1440906412,1440906413,1440906414,1440906415,1440906416]))
result={'schema':'chooguard.independent-saved-facade-review.v1','scene_sha256':scene_hash,'stair_before_sha256':sha(before),'unity_executed':False,'checks':checks,'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks)}
(OUT/'saved-facade-review.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='checks'},indent=2));print('FAILED',[c for c in checks if not c['passed']])
