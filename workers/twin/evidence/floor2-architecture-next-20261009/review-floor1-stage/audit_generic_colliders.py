"""Exact source frontage collider selection; bounded read-only fixture/public sweep."""
from pathlib import Path
import json,hashlib,sys,math
import numpy as np
from shapely.geometry import Polygon,LineString,Point
from shapely import contains_xy
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
sys.path.insert(0,'workers/twin')
from official_plan_compare import readbin
EV=Path('workers/twin/evidence/floor2-architecture-next-20261009/review-floor1-stage')
BASE=Path('.planning/2026-10-08-interior-runtime-completion/architecture/after-busanok-finishes/current-scene-structure.json');PACKET=Path('.planning/2026-10-08-interior-runtime-completion/floor-guide-layout/floor1-packet.json')
def sha(x):return hashlib.sha256(Path(x).read_bytes()).hexdigest()
s=json.loads(BASE.read_text());m={x['id']:x for x in s['meshes']};p=json.loads(PACKET.read_text());units=p['units'];spec=json.loads(Path('.planning/2026-09-24-interior-twin/specs/1f-detail.json').read_text());fronts={int(e['id'][7:9]):e for e in spec['elements'] if e['id'].startswith('1F-unit') and e['id'].endswith('-front1') and 9<=int(e['id'][7:9])<=15};sn,cs=math.sin(math.radians(16.2)),math.cos(math.radians(16.2));rows=[];patches=[]
for c in s['colliders']:
 if not c['active'] or not c['enabled'] or 'Kit_Colliders' not in c['path'] or not any(c['path'].startswith(x) for x in ['실내 트윈 마감/1F/','실내 트윈 마감/1F 공용/','실내 트윈 마감/점포 내부 1F/']):continue
 mm=m[c['mesh']];v,t=readbin(mm);wm=np.array(c['world_matrix']);w=v@wm[:3,:3].T+wm[:3,3];uv=np.c_[w[:,0]*sn+w[:,2]*cs,w[:,0]*cs-w[:,2]*sn];tv=uv[t];ys=w[t,1];face_assign=np.full(len(t),-1,int);components=[];evidence=[]
 if c['path']=='실내 트윈 마감/1F/Kit_Colliders':
  for number,e in fronts.items():
   a,b=np.array(e['geometry']['edge']);L=np.linalg.norm(b-a);ex=(b-a)/L;n=np.array([-ex[1],ex[0]]);along=(tv-a)@ex;depth=(tv-a)@n
   eligible=(abs(depth).max(1)<1e-4)&(along.min(1)>=-.0001)&(along.max(1)<=L+.0001)&(ys.min(1)>=-.0001)&(ys.max(1)<=2.4001)&(ys.max(1)>2.3999)
   inds=np.flatnonzero(eligible);face_assign[inds]=number-9;evidence.append({'boardNo':number,'element':e['id'],'triangle_ordinals':inds.tolist(),'matching_rule':'all UV vertices on exact declared storefront edge within1e-4;Y0..2.4 fixed-glazing KColRect; door gap remains excluded','source_frontline':e['geometry']['edge']})
 elif '점포 내부 1F' in c['path']:
  _,weld=np.unique(np.round(v,5),axis=0,return_inverse=True);a=weld[t[:,[0,1,2]]].ravel();b=weld[t[:,[1,2,0]]].ravel();_,labs=connected_components(coo_matrix((np.ones(len(a)),(a,b)),shape=(weld.max()+1,weld.max()+1)),directed=False);labs=labs[weld]
  for comp in np.unique(labs[t[:,0]]):
   ti=np.flatnonzero(labs[t[:,0]]==comp);vi=np.unique(t[ti]);eligible=[j for j,u in enumerate(units) if contains_xy(Polygon(u['before']['polygon']).buffer(.35),uv[vi,0],uv[vi,1]).all() and w[vi,1].min()>=-.01 and w[vi,1].max()<=2.01]
   if not eligible:continue
   j=min(eligible,key=lambda z:np.linalg.norm(uv[vi].mean(0)-np.array(units[z]['before']['polygon']).mean(0)));face_assign[ti]=j;components.append({'component':int(comp),'boardNo':j+9,'triangle_ordinals':ti.tolist(),'vertices':vi.tolist(),'uv_bounds':[uv[vi].min(0).tolist(),uv[vi].max(0).tolist()],'Y':[float(w[vi,1].min()),float(w[vi,1].max())],'basis':'source1F_KitFixturesOnly asset; full welded component inside exact ShopInterior unit polygon+.35; remains separate from44 moving model-instance colliders','semantic_fixture':'cooler' if j==0 else 'bakeryShelf' if j==3 else 'requires source layout assignment'})
 else:
  # Exact changed BOH edge known separately to root; preserve other public room/core geometry.
  a,b=np.array([[-43.64,-9.42],[-42.6,-17.1]]);line=LineString([a,b]);cent=tv.mean(1);dist=np.array([line.distance(Point(q)) for q in cent]);near=(dist<.2)&(ys.max(1)>1.)
  evidence.append({'role':'known_BOH_edge_root_owned','source_line_UV':[a.tolist(),b.tolist()],'nearby_triangles':np.flatnonzero(near).tolist()})
  for j,u in enumerate(units):
   old=Polygon(u['before']['polygon']);new=Polygon(u['after']['polygon']);withinnew=contains_xy(new.buffer(-.02),cent[:,0],cent[:,1]);withinold=contains_xy(old.buffer(-.02),cent[:,0],cent[:,1]);cross_height=(ys.min(1)<1.5)&(ys.max(1)>1.5)
   ti=np.flatnonzero((withinnew|withinold)&cross_height)
   if len(ti):evidence.append({'role':'public_faces_near_unit_not_authorized_to_move','boardNo':j+9,'triangle_ordinals':ti.tolist(),'inside_current_centroids':int((withinold[ti]).sum()),'inside_proposed_centroids':int((withinnew[ti]).sum()),'triangles_also_known_BOH':[int(x) for x in ti if near[x]],'qualification':'centroid screening only; exact source public element matching needed before movement'})
 selected=np.flatnonzero(face_assign>=0);vi=np.unique(t[selected]) if len(selected) else np.array([],int);vertex_assign=np.full(len(v),-1,int);conflict=[]
 for i in selected:
  lab=face_assign[i]
  for z in t[i]:
   if vertex_assign[z]>=0 and vertex_assign[z]!=lab:conflict.append(int(i))
   vertex_assign[z]=lab
 crossing=np.flatnonzero((vertex_assign[t]>=0).any(1)&(face_assign<0));replacement=v[vi].astype(float).copy()
 for j,u in enumerate(units):replacement[vertex_assign[vi]==j]+=np.linalg.solve(wm[:3,:3],np.array(u['world_delta']))
 row={'owner':c['path'],'global_object_id':c['global_object_id'],'mesh':c['mesh'],'asset':mm['asset'],'binary_sha256':mm['binary_sha256'],'asset_sha256':sha(mm['asset']),'world_matrix':c['world_matrix'],'source_triangles':len(t),'selected_triangles':len(selected),'selected_vertices':len(vi),'selected_by_unit':[{'boardNo':j+9,'triangles':int((face_assign==j).sum())} for j in range(7)],'evidence':evidence,'fixture_component_receipts':components,'vertex_crossing_unselected_faces':crossing.tolist(),'conflicts':conflict};rows.append(row)
 if len(vi):patches.append({'source_asset':mm['asset'],'source_asset_guid':mm['asset_guid'],'source_file_sha256':sha(mm['asset']),'source_binary_sha256':mm['binary_sha256'],'vertices':len(v),'triangles':len(t),'vertex_indices':vi.tolist(),'expected_positions':v[vi].tolist(),'replacement_positions':replacement.tolist(),'selected_triangles':selected.tolist(),'owners':[],'collider_owners':[{'path':c['path'],'global_object_id':c['global_object_id'],'world_matrix':c['world_matrix']}],'reference_only':True,'selection_proof':row['selected_by_unit'],'qualification':'Root verify semantic primitive fixture ownership before integrating fixture-only candidate; frontage candidate has exact Storefront element-line match.'})
r={'schema':'chooguard.floor1-generic-collider-readonly-audit.v1','source_inventory':str(BASE),'source_inventory_sha256':sha(BASE),'stage_packet_sha256':sha(PACKET),'reference_only':True,'production_writes':0,'generic_collider_owners':rows,'mesh_patch_candidates':patches,'remaining_gate':'root owns BOH edge and current primitive fixture semantic acceptance; preserve all other public/core colliders'}
(EV/'generic-collider-candidates.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
for row in rows:print(json.dumps({k:v for k,v in row.items() if k not in ['world_matrix','evidence','fixture_component_receipts']},ensure_ascii=False))
