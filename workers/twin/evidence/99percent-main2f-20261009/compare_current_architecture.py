import json,hashlib,re,struct
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
from shapely.geometry import LineString,Point,Polygon
from shapely.ops import unary_union
ROOT=Path('/Users/um-yunsang/CHOOGuard');O=ROOT/'workers/twin/output/99percent-main2f-20261009';E=ROOT/'workers/twin/evidence/99percent-main2f-20261009'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(n,x):(O/n).write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n')
D=json.loads((ROOT/'.planning/2026-10-09-floor2-completion-pass2/after-facades/current-inventory.json').read_text());meshes={m['id']:m for m in D['meshes']};P=json.loads((O/'main2f-source-architecture-trace.json').read_text());M=np.array(P['native_UV_to_source_pixels']);frame=np.array([[.9602936856769431,0,.2789911060392293],[0,1,0],[-.2789911060392293,0,.9602936856769431]])
def px(v):return np.c_[np.atleast_2d(v),np.ones(len(np.atleast_2d(v)))]@M
cache={}
def geometry(r):
 m=meshes[r['mesh']];b=ROOT/m['binary']
 if m['id']not in cache:
  blob=b.read_bytes();nv,ni,nn=struct.unpack_from('<iii',blob);v=np.frombuffer(blob,dtype='<f4',count=nv*3,offset=12).reshape(-1,3).copy();ix=np.frombuffer(blob,dtype='<i4',count=ni,offset=12+nv*12).reshape(-1,3).copy();cache[m['id']]=(v,ix)
 v,ix=cache[m['id']];w=np.c_[v,np.ones(len(v))]@np.array(r['world_matrix']).T;v=w[:,:3]@frame;return v[:,[2,0,1]],ix
native=np.load(ROOT/'workers/twin/output/floor2-official-pdf-registration-20261009/native-original-section-UV.npy');nativepx=px(native.reshape(-1,2)).reshape(-1,2,2)
source=ROOT/P['source'];im=Image.open(source).convert('RGB').resize((2524,1984),Image.Resampling.NEAREST);d=ImageDraw.Draw(im)
for seg in nativepx:
 if np.min(seg[:,1])>=267 and np.max(seg[:,1])<489:d.line([tuple(x*4)for x in seg],fill='#206cbc',width=1)
gensegs=[];owner_proofs=[]
for r in D['renderers']:
 if not r['path'].startswith('실내 트윈 마감/2F 편의/Kit_'):continue
 v,ix=geometry(r);t=v[ix];cross=(t[:,:,2].min(1)<8.25)&(t[:,:,2].max(1)>8.25)
 for tri in t[cross]:
  points=[]
  for a,b in zip(tri,np.roll(tri,-1,axis=0)):
   if(a[2]-8.25)*(b[2]-8.25)<0:points.append(a[:2]+(b[:2]-a[:2])*(8.25-a[2])/(b[2]-a[2]))
  if len(points)==2:gensegs.append(px(points))
 m=meshes[r['mesh']];owner_proofs.append({'renderer':r,'mesh':m,'current_asset_sha256':sha(ROOT/m['asset']),'asset_matches_export':sha(ROOT/m['asset'])==m['asset_sha256']})
for seg in gensegs:
 if np.min(seg[:,1])>=267 and np.max(seg[:,1])<489:d.line([tuple(x*4)for x in seg],fill='#e38423',width=2)
for r in P['wall_and_boundary_runs']:
 if r['kit_draft_candidate']:d.line([tuple(np.array(x)*4)for x in r['source_segment_pixels']],fill='#c51534',width=2)
im.crop((180,1060,2524,1960)).save(O/'main-current-topology-source-overlay.png')
# Reuse old canonical polygons strictly as model evidence, never fitting controls.
services=ROOT/'content/world/observed-interiors/services-2f.json';S=json.loads(services.read_text());old=[x for x in S['elements'] if x['type']=='Room'and x['id'].startswith('RM-2F-S-')and x['id']!='RM-2F-S-철도경찰'];oldpolys=[Polygon(px(x['geometry']['polygon']))for x in old];region=unary_union(oldpolys+[Polygon([[461,273],[566,273],[566,336],[461,336]])]).buffer(2)
# Exact current saved YAML component ownership: MR is not MF. No ID arithmetic.
scene=ROOT/'Assets/ChooGuard/Scenes/FpsStation.unity';scene_text=scene.read_text();blocks={int(i):(int(c),s)for c,i,s in re.findall(r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)',scene_text,re.M|re.S)}
def components(gid):
 owner=int(gid.split('-')[-2]);cl,s=blocks[owner];go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',s).group(1));cs=[int(x)for x in re.findall(r'component: \{fileID: (\d+)\}',blocks[go][1])];return go,[{'file_id':x,'class_id':blocks[x][0]}for x in cs]
selection=[]
for r in D['renderers']+D['colliders']:
 if not r['path'].startswith('실내 트윈 마감/2F 편의/Kit_')or not r.get('mesh'):continue
 # Include only generated static room finish contributors, not old operators/props.
 if not any(t in r['path']for t in ['RoomWall','Floor_','Ceiling_','CeilingGrid','Slab_','Trim_','ShadowCap','Clad_','Joint','Kit_Colliders']):continue
 v,ix=geometry(r);t=v[ix];pixels=px(t[:,:,:2].reshape(-1,2)).reshape(-1,3,2);low=t[:,:,2].min(1);high=t[:,:,2].max(1);candidate=(low>=6.75)&(high<=10.6)&(pixels[:,:,0].min(1)>440)&(pixels[:,:,0].max(1)<615)&(pixels[:,:,1].min(1)>268)&(pixels[:,:,1].max(1)<341)
 ids=[];boundary=[]
 for i in np.flatnonzero(candidate):
  poly=Polygon(pixels[i]);pts=[Point(p)for p in pixels[i]]
  if all(region.covers(p)for p in pts):ids.append(int(i))
  elif poly.intersects(region):boundary.append(int(i))
 if not ids and not boundary:continue
 go,cs=components(r['global_object_id']);kind='MeshCollider'if r in D['colliders']else'MeshFilter';exact=[c['file_id']for c in cs if c['class_id']==(64 if kind=='MeshCollider'else 33)]
 m=meshes[r['mesh']];selection.append({'observed_inventory_owner':r['global_object_id'],'observed_inventory_owner_kind':'MeshCollider'if r in D['colliders']else'MeshRenderer','exact_geometry_component_kind':kind,'exact_geometry_component_file_ids':exact,'same_gameobject_file_id':go,'path':r['path'],'world_matrix':r['world_matrix'],'mesh':m,'current_asset_matches_export':sha(ROOT/m['asset'])==m['asset_sha256'],'selected_inside_triangle_indices':ids,'boundary_crossing_triangle_indices':boundary,'selection_status':'draft only, requires exact full-channel export and retention review before mutation','scope':'Old generated static service finish only. Native source floor/shell/core and all current operators preserved.'})
nativelines=unary_union([LineString(x)for x in nativepx if np.max(x[:,1])>=270 and np.min(x[:,1])<=485]);genlines=unary_union([LineString(x)for x in gensegs if np.max(x[:,1])>=270 and np.min(x[:,1])<=485]);diagnostics=[]
for r in P['wall_and_boundary_runs']:
 a,b=np.array(r['source_segment_pixels']);q=np.linspace(a,b,max(2,int(np.linalg.norm(a-b)*2)+1));dn=np.array([nativelines.distance(Point(x))for x in q]);dg=np.array([genlines.distance(Point(x))for x in q]);diagnostics.append({'id':r['id'],'source_kind':r['kind'],'region':r['region'],'proposed':r['kit_draft_candidate'],'sample_count':len(q),'native_section_within_2p5px_fraction':float(np.mean(dn<=2.5)),'generated_service_section_within_2p5px_fraction':float(np.mean(dg<=2.5)),'native_median_distance_px':float(np.median(dn)),'generated_median_distance_px':float(np.median(dg)),'interpretation':'Section line distance diagnoses model disagreement only; source visible topology remains authoritative and no site metric precision is implied.'})
dump('current-topology-comparison.json',{'source_packet_sha256':sha(O/'main2f-source-architecture-trace.json'),'inventory_path':str((ROOT/'.planning/2026-10-09-floor2-completion-pass2/after-facades/current-inventory.json').relative_to(ROOT)),'inventory_scene_epoch':D['scene_file_sha256'],'comparison_section_Y':8.25,'owner_asset_proofs':owner_proofs,'native_section_source':'floor2-official-pdf-registration-20261009/native-original-section-UV.npy','source_run_diagnostics':diagnostics,'old_room_polygons':[{'id':x['id'],'source_projected_polygon':px(x['geometry']['polygon']).tolist(),'basis':'Current canonical model, not independent source fit'}for x in old],'classification':'Plan topology and unmeasured model differ; diagnostic geometric proximity alone does not decide correctness.'})
dump('east-service-generated-contributor-selection-draft.json',{'schema':'chooguard.source-correction-contributor-selection-draft.v1','scene_read_sha256':sha(scene),'source_packet_sha256':sha(O/'main2f-source-architecture-trace.json'),'source_correction_region_pixel_exterior':list(region.exterior.coords),'canonical_old_room_ids':[x['id']for x in old],'canonical_spec_path':str(services.relative_to(ROOT)),'canonical_spec_sha256':sha(services),'selection':selection,'preserve':['All original Ground_Floor and MainShell source geometry','All original and current door/core operators until separately source-reviewed replacement','Matchinghall generated contributors and floor axis','철도경찰 room outside this bounded nine-room comparison'],'not_installed':True,'required_next_action':'Root exact full-channel export of listed geometry component IDs, review crossing triangles and generated semantic provenance, then rebased retention. This draft alone authorizes no wholesale owner removal.'})
print(json.dumps({'generated_section_segments':len(gensegs),'selected_channels':len(selection),'inside_triangles':sum(len(x['selected_inside_triangle_indices'])for x in selection),'crossing_triangles':sum(len(x['boundary_crossing_triangle_indices'])for x in selection),'all_selected_assets_match':all(x['current_asset_matches_export']for x in selection)}))
