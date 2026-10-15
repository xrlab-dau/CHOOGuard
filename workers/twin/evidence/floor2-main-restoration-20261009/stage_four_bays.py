"""Offline exact current-source four-bay architecture patch. Owned output/evidence only."""
from pathlib import Path
import sys,json,hashlib,re,yaml
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
sys.path.insert(0,str(Path('workers/twin').resolve()));from official_plan_compare import readbin
E=Path(__file__).resolve().parent;O=Path('workers/twin/output/floor2-main-restoration-20261009');O.mkdir(parents=True,exist_ok=True)
s=json.loads((E/'current-inventory.json').read_text());mm={m['id']:m for m in s['meshes']};proposal=json.loads((E/'prior-proposal.json').read_text());units=proposal['units'];scenecomp=json.loads((E/'scene-components.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
ref=next(r for r in s['renderers'] if r['path']=='맞이방 · 원본 정합/Shop_다인명가_Walls');FRAME=np.array(ref['world_matrix'])[:3,:3];INV=np.linalg.inv(FRAME)
def uvy(world):
 p=world@INV.T;return p[:,[2,0,1]]
def toworld(q):return q[:,[1,2,0]]@FRAME.T
def mapq(q,u):
 out=q.copy();m=u['native_mapping'];out[:,0]=out[:,0]*m['u_scale']+m['u_bias'];out[:,1]+=m['v_translation'];return out
def anchor(u,front):return np.array([(u['current_u'][0] if u['face']=='-u' else u['current_u'][1]) if front else np.mean(u['current_u']),np.mean(u['current_v']),7.])
def delta(u,front):
 a=anchor(u,front);return mapq(a[None],u)[0]-a
rows={};rejects=[];sourceproof=[];rgroup={};cache={}
for r in s['renderers']+s['colliders']:
 path=r['path'];mid=r.get('mesh')
 if not mid or not path.startswith(('맞이방 · 원본 정합/','실내 트윈 마감/2F 본관/','실내 트윈 마감/점포 내부 2F 본관/')) or '/문 · ' in path:continue
 m=mm[mid]
 if not m.get('binary'):continue
 rgroup.setdefault(m['asset'],[]).append(r)
 if m['asset'] in rows:continue
 if mid not in cache:cache[mid]=readbin(m)
 v,t=cache[mid];M=np.array(r['world_matrix']);w=v@M[:3,:3].T+M[:3,3];q=uvy(w);assigned=np.full(len(v),-1,int);selected=np.zeros(len(t),bool);unitrows=[]
 for i,u in enumerate(units):
  lo=np.array([u['current_u'][0]-.35,u['current_v'][0]-.35,6.95]);hi=np.array([u['current_u'][1]+.35,u['current_v'][1]+.35,10.6]);fs=np.flatnonzero(((q[t]>=lo)&(q[t]<=hi)).all((1,2)));vi=np.unique(t[fs])
  if len(vi):
   if (assigned[vi]>=0).any():rejects.append({'owner':path,'reason':'source vertices assigned to multiple tenancies'});continue
   assigned[vi]=i;selected[fs]=True;unitrows.append({'guide_number':u['guide_number'],'source_name':u['current_unit_name'],'selected_triangle_ordinals':fs.tolist(),'selected_vertex_indices':vi.tolist()})
 ids=np.flatnonzero(assigned>=0)
 if not len(ids):continue
 crossing=np.flatnonzero((assigned[t]>=0).any(1)&~selected)
 if len(crossing):rejects.append({'owner':path,'reason':'selected vertex also used by retained face','source_triangle_ordinals':crossing.tolist()});continue
 mode='affine_bay_depth' if path.endswith('_Walls') or m['name']=='Majibang_ShopCeiling' else 'rigid_ceiling_fixture_components' if m['name']=='Majibang_ShopLED' else 'rigid_frontage_translation'
 target=q.copy();components=[]
 if mode=='affine_bay_depth':
  for i,u in enumerate(units):target[assigned==i]=mapq(q[assigned==i],u)
 elif mode=='rigid_frontage_translation':
  for i,u in enumerate(units):target[assigned==i]=q[assigned==i]+delta(u,True)
 else:
  _,weld=np.unique(np.round(v[ids],5),axis=0,return_inverse=True);iv=np.full(len(v),-1,int);iv[ids]=weld;ed=iv[t[selected]];a,b=ed.ravel(),ed[:,[1,2,0]].ravel();_,labels=connected_components(coo_matrix((np.ones(len(a)),(a,b)),shape=(weld.max()+1,weld.max()+1)),directed=False)
  for label in np.unique(labels):
   vi=ids[labels[weld]==label];uis=set(assigned[vi]);assert len(uis)==1;u=units[next(iter(uis))];d=delta(u,False);target[vi]=q[vi]+d;components.append({'unit':u['guide_number'],'vertex_indices':vi.tolist(),'source_anchor_UV':anchor(u,False)[:2].tolist(),'native_rigid_delta_UV':d[:2].tolist()})
 replacement=((toworld(target[ids])-M[:3,3])@np.linalg.inv(M[:3,:3]).T).astype('<f4');f=Path(m['asset']);body=f.read_bytes();a=yaml.safe_load(body.decode().split('Mesh:\n',1)[1]);raw=bytes.fromhex(a['m_VertexData']['_typelessdata']);nv=a['m_VertexData']['m_VertexCount'];stride=len(raw)//nv;actualt=np.frombuffer(bytes.fromhex(a['m_IndexBuffer']),'<u4' if a['m_IndexFormat']==1 else '<u2').reshape(-1,3);assert np.array_equal(actualt,t)
 row={'source_asset':m['asset'],'source_asset_guid':m['asset_guid'],'source_file_sha256':sha(f),'source_binary_sha256':m['binary_sha256'],'vertices':len(v),'triangles':len(t),'vertex_indices':ids.tolist(),'expected_positions':v[ids].tolist(),'replacement_positions':replacement.tolist(),'selected_triangles':np.flatnonzero(selected).tolist(),'mapping_mode':mode,'unit_contributor_masks':unitrows,'rigid_components':components,'source_selected_vertex_channel_bytes_sha256':hashlib.sha256(b''.join(raw[i*stride:(i+1)*stride] for i in ids)).hexdigest(),'source_selected_index_bytes_sha256':hashlib.sha256(t[selected].astype('<u4').tobytes()).hexdigest(),'source_channels':a['m_VertexData']['m_Channels'],'source_submeshes':a['m_SubMeshes'],'source_index_format':a['m_IndexFormat'],'source_full_vertex_bytes_sha256':hashlib.sha256(raw).hexdigest()}
 rows[m['asset']]=row
# All exact MeshFilter/MeshCollider instances; never infer by nearby display name.
for key,row in rows.items():
 users=[r for r in s['renderers']+s['colliders'] if mm.get(r.get('mesh'),{}).get('asset')==key];row['owners']=[r for r in users if r.get('type')!='MeshCollider'];row['collider_owners']=[r for r in users if r.get('type')=='MeshCollider'];assert all(r['world_matrix']==users[0]['world_matrix'] for r in users)
 sourceproof.append({'source_asset':key,'file_sha256':sha(key),'meta_sha256':sha(key+'.meta'),'binary_sha256':row['source_binary_sha256'],'renderer_owners':row['owners'],'collider_owners':row['collider_owners']})
motions=[]
for u in units:
 name=u['current_unit_name'];door=next(d for d in scenecomp['doors'] if d['path'].endswith('문 · '+name+' 출입문'));d=toworld(delta(u,True)[None])[0]
 motions.append({'kind':'door','door_element':door['fields']['Element'],'path':door['path'],'unit':u['guide_number'],'world_delta':d.tolist(),'source_transform_id':door['transform_id'],'source_component_id':door['component_id'],'preserve_station_door_fields_and_leaf_local_meshes':True})
 for path,front in [('맞이방 · 원본 정합/'+name+' fill light',False),('실내 트윈 마감/2F 본관/Sign · '+name,True)]:
  node=next(n for n in scenecomp['named_components'] if n['path']==path);d=toworld(delta(u,front)[None])[0];motions.append({'kind':'path','path':path,'unit':u['guide_number'],'world_delta':d.tolist(),'source_transform_id':node['transform_id'],'source_gameobject_id':node['gameobject_id'],'source_local_transform':node['transform'],'preserve_material_text_light_fields':True})
packet={'schema':'chooguard.floor-guide-layout-patch.v1','batch_id':'2f-entry','floor':2,'work_dir':'.planning/2026-10-09-floor2-guide-restoration','scene':s['scene'],'scene_sha256':s['scene_file_sha256'],'guide':proposal['source_guide']['path'],'guide_sha256':proposal['source_guide']['sha256'],'proposal':str(E/'prior-proposal.json'),'proposal_sha256':sha(E/'prior-proposal.json'),'reference_only':True,'site_accuracy_verified':False,'method':'Relocate four legacy source tenants into native entrance side bays. Contract only wall/ceiling depth; every storefront, downlight/ring, LED, door, sign and light keeps its geometry and binding. No new interior fixtures.','units':[{'boardNo':u['guide_number'],'name':u['current_unit_name'],'before':{'u':u['current_u'],'v':u['current_v']},'after':{'u':u['proposed_u'],'v':u['proposed_v']},'mapping':u['native_mapping'],'field_measurement':False} for u in units],'mesh_patches':list(rows.values()),'movable_transforms':motions,'rejected_owners':rejects,'protected':['native MainShell/EV/entrance/core and floor faces','flowers/Boksoondoga current placement/finish/props','all1F production geometry/guide layers','all103door elements/local leaf fields/operating bindings','all other tenant contributor indices/channels/materials/owner poses'],'source_inventory':str(E/'current-inventory.json'),'source_inventory_sha256':sha(E/'current-inventory.json')}
(O/'four-bay-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2)+'\n');(E/'four-bay-source-receipts.json').write_text(json.dumps(sourceproof,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'meshes':len(rows),'vertices':sum(len(q['vertex_indices']) for q in rows.values()),'faces':sum(len(q['selected_triangles']) for q in rows.values()),'renderers':sum(len(q['owners']) for q in rows.values()),'colliders':sum(len(q['collider_owners']) for q in rows.values()),'motions':len(motions),'rejected':rejects,'modes':[(Path(q['source_asset']).name,q['mapping_mode']) for q in rows.values()]},ensure_ascii=False,indent=2))
