"""Worker-only native topology registration and bounded contributor draft.
No Unity or source/canonical writes. Source pixels are manual raster readings.
"""
import json, hashlib, struct
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from shapely.geometry import Polygon, Point, LineString
from shapely.ops import unary_union

ROOT=Path('/Users/um-yunsang/CHOOGuard')
EV=ROOT/'workers/twin/evidence/floor1-provided-pdf-20261009'
OUT=ROOT/'workers/twin/output/floor1-provided-pdf-20261009'
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def dump(n,d): (OUT/n).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
scan=json.loads((OUT/'native-original-1f-scan.json').read_text())
invpath=ROOT/'.planning/2026-10-09-floor2-completion-pass2/after-facades/current-inventory.json'
inv=json.loads(invpath.read_text()); meshes={x['id']:x for x in inv['meshes']}
public=json.loads((ROOT/'content/world/observed-interiors/public-1f.json').read_text())
old=json.loads((OUT/'correction-packet.json').read_text())
toilets=json.loads((OUT/'toilet-source-controls.json').read_text())
floor_r=next(r for r in inv['renderers'] if r['path']=='1층 · 원본 정합/Ground_Floor')
F=np.linalg.inv(np.array(floor_r['world_matrix']))
def binary(mid):
 m=meshes[mid];raw=(ROOT/m['binary']).read_bytes();assert sha(ROOT/m['binary'])==m['binary_sha256']
 nv,ni,nn=struct.unpack_from('<iii',raw)
 return np.frombuffer(raw,'<f4',nv*3,12).reshape(nv,3).astype(float),np.frombuffer(raw,'<i4',ni,12+nv*12).reshape(-1,3)
def uvy(r,v): return (np.c_[v,np.ones(len(v))]@np.array(r['world_matrix']).T@F.T)[:,[2,0,1]]
def ident(r):
 m=meshes[r['mesh']]
 return {k:r[k]for k in ['path','global_object_id','mesh','world_matrix']}|{k:m[k]for k in ['asset','asset_guid','binary','binary_sha256','asset_sha256']if k in m}
def control(id,pix,row,seg,end):
 r=scan['rows'][row]
 return {'id':id,'source_pixel':pix,'native_UV':r['section_segments_UV'][seg][end],
 'original_source_id':r['source']['source_id'],'original_source_path':r['source']['source_path'],
 'original_object_matrix_world':r['source'].get('matrix_world'),'scan_row':row,'section_segment':seg,'endpoint':end,
 'semantic_correspondence':'Original 1F outer wall junction at Y1.5; source raster wall junction, not generated kit'}
controls=[control('east-service-rear-return',[947,90],193,15,1),
 control('east-lower-return',[946,254],193,6,1),
 control('west-diagonal-knee',[91,274],4855,28,1)]
X=np.array([x['source_pixel']+[1]for x in controls],float);Y=np.array([x['native_UV']for x in controls]);A=np.linalg.solve(X,Y)
def project(q):
 q=np.atleast_2d(q);return (np.c_[q,np.ones(len(q))]@A).tolist()
def inverse(q):return ((np.asarray(q)-A[2])@np.linalg.inv(A[:2])).tolist()
held=[]
for id,pix,row,limit in [('terminal-rear-corridor-column',[942,53],3846,None),('terminal-service-rear-column-cap',[942,90],1390,6)]:
 r=scan['rows'][row];p=np.array(r['section_segments_UV'][:limit]).reshape(-1,2);c=(p.min(0)+p.max(0))/2
 pp=np.array(inverse(c));pr=np.array(project([pix])[0])
 held.append({'id':id,'source_pixel':pix,'original_section_bounds_center_UV':c.tolist(),
 'section_shape': 'complete rectangular column' if limit is None else 'open terminal column cap; bounded first six segments, not whole long component centre',
 'original_source_id':r['source']['source_id'],'original_source_path':r['source']['source_path'],
 'scan_row':row,'section_segment_selection':'all'if limit is None else list(range(limit)),
 'projected_source_pixel':pp.tolist(),'residual_pixels':float(np.linalg.norm(pp-pix)),
 'residual_model_units_reference_only':float(np.linalg.norm(pr-c)),
 'held_out_from_fit':True,'semantic_correspondence':'Terminal column at the two distinct corridor rear/room rear lines; not repeated ordinal matching'})
front=[]
for name,path,pix in [('east-front-entry-jamb','ROOT/g56/g0/g0/g2/i869862/g8/g0/geometry',[313,368]),('west-front-entry-jamb','ROOT/g56/g0/g0/g2/i869865/g7/g1/geometry',[447,368])]:
 r=next(x for x in scan['rows']if x['source']['source_path']==path);b=np.array(r['native_UVY_bounds']);c=b.mean(0)[:2]
 front.append({'id':name,'source_pixel_approx':pix,'original_source_id':r['source']['source_id'],'original_source_path':path,'native_UV_bounds_center':c.tolist(),'residual_model_units_reference_only':float(np.linalg.norm(c-project([pix])[0])),'status':'global affine front-region disagreement; no front-room reconstruction authorized from this fit'})
reg={'schema':'chooguard.source-native-topology-registration.v1','source_raster_size':[960,572],
 'source_raster':str((EV/'embedded-architecture-white.png').relative_to(ROOT)),
 'source_raster_sha256':sha(EV/'embedded-architecture-white.png'),
 'source_blend':scan['source_blend'],'source_blend_sha256':scan['source_blend_sha256'],
 'section_y':1.5,'pixel_to_native_UV_affine':A.tolist(),'training_controls':controls,
 'training_residual_pixels':0.0,'training_residual_note':'Three-point affine interpolates by construction; this is not independent accuracy.',
 'held_out_controls':held,'held_out_pixel_rms':float(np.sqrt(np.mean([x['residual_pixels']**2 for x in held]))),
 'front_region_disagreements':front,'accepted_for':'Relative source localization of rear service strip and two main WC clusters only.',
 'native_independent':True,'global_metric_scale_verified':False,'site_accuracy_verified':False,
 'source_pixel_tolerance':3,'global_warp_authorized':False,
 'registration_limits':['The drawing is raster and unmeasured. Pixel tolerance is source transcription uncertainty, not a site survey tolerance.','Rear external corridor boundary y31 is distinct from service/MainShell rear line y90.','Two heldouts localize the east terminal corridor; west WC relies on unique original knee and original diagonal outline agreement, not independent local three-point metric fit.','Generated shop or Naver stair kit positions are not fit controls.','Front entrance region disagreement remains; no front architecture is rebuilt from this affine.'],
 'original_native_source_frame_rotation':scan['source_frame_rotation'],'original_native_source_frame_translation':scan['source_frame_translation'],'station_frame_world_rotation':scan['station_frame_world_rotation']}
dump('native-topology-registration.json',reg)
# Trace every room edge and every visible jamb separately. No hidden room additions.
rooms=old.get('rooms',old.get('service_rooms',[]))
if not rooms:
 rooms=[{'id':'pdf1f-disaster-center','label':'방재센터','source_polygon_pixels_approx':[[44,90],[175,90],[175,173],[87,173],[44,118]]}, {'id':'pdf1f-military-consultation','label':'상담실 / 군장병 편의시설','source_polygon_pixels_approx':[[175,90],[313,90],[313,173],[175,173]]},{'id':'pdf1f-electrical-room','label':'전기실','source_polygon_pixels_approx':[[313,90],[582,90],[582,132],[313,132]]}]
doors=[{'id':'disaster-public-double','room':'pdf1f-disaster-center','source_jamb_pixels':[[141,173],[154,173]],'swing_source_pixel':[0,-1]}, {'id':'military-public-double','room':'pdf1f-military-consultation','source_jamb_pixels':[[251,173],[267,173]],'swing_source_pixel':[0,-1]}, {'id':'electric-rear-double-a','room':'pdf1f-electrical-room','source_jamb_pixels':[[373,90],[386,90]],'swing_source_pixel':[0,-1]}, {'id':'electric-rear-double-b','room':'pdf1f-electrical-room','source_jamb_pixels':[[508,90],[520,90]],'swing_source_pixel':[0,-1]}]
wc=[]
for cl in toilets['source_clusters']:
 if not cl['attached_to_main_hall']:continue
 for id,p in cl['source_room_polygons_pixels_approx'].items():
  wc.append({'id':id,'label':'WC '+id,'source_polygon_pixels_approx':p,'existing_height_reference':4.4 if id.startswith('left') else 4.8,'gender_assignment':'unresolved; maintain upper/lower source identity'})
v,t=binary(floor_r['mesh']);q=uvy(floor_r,v)
floor=unary_union([Polygon(q[f,:2])for f in t if np.max(np.abs(q[f,2]))<.002 and Polygon(q[f,:2]).area>1e-7])
segments=[]
for room in rooms+wc:
 p=room['source_polygon_pixels_approx'];room['native_polygon_UV']=project(p)
 po=Polygon(room['native_polygon_UV']);room['source_pixel_tolerance']=3
 room['ground_floor_supported_fraction']=po.intersection(floor).area/po.area
 room['height_thickness_status']='Retain existing generated height/thickness as unmeasured rendering parameters; raster establishes no height.'
 for i,(a,b) in enumerate(zip(p,p[1:]+p[:1])):
  s={'id':room['id']+'-edge-'+str(i),'room_id':room['id'],'source_segment_pixels':[a,b],'native_segment_UV':project([a,b]),'source_evidence':'embedded-architecture-white.png; this exact edge highlighted in native-room-traces.png','source_pixel_tolerance':3,'native_original_mutation_authorized':False,'wall_status':'trace; source-model relative, not surveyed'}
  if a[1]==b[1]==90:s['wall_status']='existing original rear wall alignment; preserve original native contributor and overlay only where appropriate'
  segments.append(s)
for d in doors:
 d['visible_leaves']=2;d['native_jamb_UV']=project(d['source_jamb_pixels']);d['source_pixel_tolerance']=3
 d['native_swing_UV']=(np.array(d['swing_source_pixel'])@A[:2]).tolist();d['native_original_door_or_wall_cut_authorized']=False
 d['swing_status']='Diagram double leaf quarter arcs; orientation relative to drawing, no measured leaf/hinge dimensions.'
fixtures=[]
for f in toilets['fixtures']:
 n={k:v for k,v in f.items()if k not in ['proposed_center_UV','current_toilet_footprint_membership','implementation_note']};n['native_center_UV']=project([f['source_center_pixel']])[0]
 row=next(r for r in toilets['source_stall_rows']if r['room']==f['source_room']);heading=np.array(row['pan_heading_source_pixel'])@A[:2];heading/=np.linalg.norm(heading)
 n['native_front_UV']=heading.tolist();n['asset_reuse']='Existing KWc visual helper; standard visual geometry dimensions are unmeasured, source establishes symbol centre/count/orientation only.';fixtures.append(n)
rows=[]
for row in toilets['source_stall_rows']:
 n=row.copy();n['native_backline_UV']=project(row['row_backwall_pixels_approx']);n['native_frontline_UV']=project(row['row_front_pixels_approx']);rows.append(n)
packet={'schema':'chooguard.pdf-1f-correction-draft.v2','production_installed':False,'site_accuracy_verified':False,
 'registration':'workers/twin/output/floor1-provided-pdf-20261009/native-topology-registration.json',
 'native_source_ownership':'Preserve original Ground_Floor, MainShell, original columns, stairs/lifts and original door geometry.',
 'service_rooms':rooms,'main_WC_rooms':wc,'wall_segments':segments,'service_doors':doors,'small_WC_symbols':fixtures,'stall_row_controls':rows,
 'excluded_detached_detail':{'rooms':2,'reason':'No connector/location/source floor identity; do not project inset through main affine.'},
 'untranscribed_sanitary_symbols':'No accessible pans/urinals/basins counted or installed from unreadable symbols.',
 'military_lounge':{'label':'군인 맞이방','source_label_pixel':[281,193],'space_type':'open labelled area; no new enclosed room'},
 'partition_trace_limit':'Regular stall rows have five countable pan symbols. Individual thin stall divider positions require pixel review; no evenly spaced stall inference is approved.',
 'source_authenticity_and_rights':'Original user PDF and official 2026 same embedded architecture; rights unverified, reference use only.'}
dump('implementable-correction-packet.json',packet)
# Select ONLY generated contributors within old WC footprints or the exact old inferred rear clad run.
oldwc=[e for e in public['elements']if e['type']=='ToiletRoom'];oldpolys=[Polygon(e['geometry']['polygon'])for e in oldwc]
wcunion=unary_union(oldpolys);boundaries=unary_union([p.boundary for p in oldpolys])
clad=LineString([[21.1,-5],[68.8,-5]])
select=[]
names=['Kit_Floor_floor_toilet_tile','Kit_RoomWall_wall_toilet_tile_300x600','Kit_RoomWall_벽_내장','Kit_CeilingGrid','Kit_Ceiling_Kit_GridWhite','Kit_Clad_wall_painted_gypsum_fine','Kit_Joint']
targets=[r for r in inv['renderers']if '/1F 공용/'in r['path']and r['path'].split('/')[-1]in names]
targets+= [c for c in inv['colliders']if c['path']=='실내 트윈 마감/1F 공용/Kit_Colliders'and c['type']=='MeshCollider']
for r in targets:
 v,t=binary(r['mesh']);q=uvy(r,v);ids=[];by={};cladids=[]
 for i,f in enumerate(t):
  p=q[f];vv=p[:,:2];why=[]
  # Three vertices must be within one same old room. Keep crossing triangles for manual review.
  if p[:,2].min()>=-.03 and p[:,2].max()<=4.85:
   for e,po in zip(oldwc,oldpolys):
    if all(po.buffer(.18).covers(Point(z))for z in vv):
     # Wall batches require boundary proximity; floor/ceil/collider use actual room footprint.
     if 'RoomWall'in r['path'] and not all(po.boundary.distance(Point(z))<=.18 for z in vv):continue
     why.append(e['id'])
  if why:ids.append(i);by[str(i)]=why
  if any(s in r['path']for s in ['Kit_Clad_wall_painted','Kit_Joint','Kit_Colliders']) and p[:,2].min()>=-.03 and p[:,2].max()<=6.75 and all(clad.distance(Point(z))<=.10 for z in vv):cladids.append(i)
 if ids or cladids:
  select.append({'owner':ident(r),'owner_component':'MeshCollider'if r.get('type')=='MeshCollider'else'MeshFilter','original_triangle_count':len(t),'old_wc_triangle_ordinals':ids,'old_wc_triangle_recipe_attribution':by,'rear_clad_triangle_ordinals':cladids,'selected_triangle_ordinals':sorted(set(ids+cladids)),
  'selection_basis':'All three triangle vertices within one exact old canonical WC footprint +0.18 source shell allowance; RoomWall vertices also within0.18 of that old boundary. Rear clad all vertices within0.10 of exact canonical strip [21.1,-5]→[68.8,-5].',
  'status':'bounded candidate selection; root complete-channel + exact contributor ownership check before deletion',
  'required_full_channel_digest_export':True,'preservation':'Clone original mesh, change submesh indices only, retain positions/normals/tangents/colors/UV0..7 and all unselected triangles exactly.'})
dump('bounded-contributor-selection.json',{'scene_sha256':inv['scene_file_sha256'],'inventory':str(invpath.relative_to(ROOT)),'inventory_sha256':sha(invpath),'owners':select,'protected_original_native_owners':['Ground_Floor','MainShell','original source columns/cores/stairs/lifts/doors'],'selection_is_final_authorization':False,'reexport_required_if_source_asset_or_owner_frame_changed':True})
# Evidence overlays remain source pixels and original sections, not scene mutation.
im=Image.open(EV/'embedded-architecture-white.png').convert('RGB');dr=ImageDraw.Draw(im)
for r in rooms+wc:dr.line([tuple(x)for x in r['source_polygon_pixels_approx']+[r['source_polygon_pixels_approx'][0]]],fill=(220,30,0),width=2)
for f in fixtures:
 x,y=f['source_center_pixel'];dr.ellipse((x-2,y-2,x+2,y+2),outline=(0,100,220),width=1)
for d in doors:dr.line([tuple(x)for x in d['source_jamb_pixels']],fill=(0,170,0),width=3)
for c in controls:
 x,y=c['source_pixel'];dr.ellipse((x-4,y-4,x+4,y+4),outline=(190,0,200),width=2)
im.save(EV/'native-room-traces.png')
under=Image.open(EV/'embedded-architecture-white.png').convert('RGB');dd=ImageDraw.Draw(under)
for row in [193,4855,3846,1390]:
 for seg in scan['rows'][row]['section_segments_UV']:
  p=inverse(seg);dd.line([tuple(x)for x in p],fill=(0,120,210),width=1)
for h in held:
 x,y=h['projected_source_pixel'];dd.ellipse((x-3,y-3,x+3,y+3),outline=(220,0,160),width=2)
under.save(EV/'native-original-heldout-overlay.png')
print(json.dumps({'affine':A.tolist(),'heldout_RMS_pixels':reg['held_out_pixel_rms'],'heldouts':held,'room_supported_fractions':[(r['id'],r['ground_floor_supported_fraction'])for r in rooms+wc],'contributors':[(x['owner']['path'],len(x['old_wc_triangle_ordinals']),len(x['rear_clad_triangle_ordinals']))for x in select]},ensure_ascii=False))
